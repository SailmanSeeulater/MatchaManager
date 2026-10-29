using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds Assets/Scenes/Main.unity from the exported Aseprite layers.
// Menu: Matcha > Build Scene. Re-run after Tools/export_art.sh.
public static class MatchaSceneBuilder
{
    const string LayerDir = "Assets/Art/Layers/";
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string SnackDir = "Assets/Data/Snacks";
    const string AudioDir = "Assets/Audio";
    const string IconDir = "Assets/Art/Icons";
    const int RefWidth = 480, RefHeight = 270;

    // text sizes: the pixel font is crisp at whole multiples of its 7px line
    const float Px = PixelFontBuilder.PointSize, Px2 = Px * 2;

    static readonly Color Ink = Hex("1e1a2b");
    static readonly Color Cream = Hex("f3ead6");
    static readonly Color MatchaDark = Hex("2e5222");

    [Serializable] class LayerRect { public int x, y, w, h; }
    [Serializable] class LayerFrame { public string filename; public LayerRect spriteSourceSize; }
    [Serializable] class LayerData { public LayerFrame[] frames; }

    static Dictionary<string, LayerRect> rects;
    static TMP_FontAsset pixelFont;

    [MenuItem("Matcha/Build Scene")]
    public static void Build()
    {
        LoadRects();
        pixelFont = PixelFontBuilder.Build();
        var difficulties = EnsureDifficulties();
        var balance = difficulties[1]; // medium until a game picks one
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.AddComponent<AudioListener>();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5;
        canvas.pixelPerfect = true;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100;
        canvasGo.AddComponent<GraphicRaycaster>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        // fixed 480x270 "screen" that everything is laid out in, in Aseprite pixel coords
        var screen = new GameObject("Screen", typeof(RectTransform)).GetComponent<RectTransform>();
        screen.SetParent(canvasGo.transform, false);
        screen.sizeDelta = new Vector2(RefWidth, RefHeight);

        AddImage(screen, "Back_Background");

        // --- Shop (top-left) ---
        var shop = Group(screen, "Shop");
        AddImage(shop, "Shop_Wall");
        AddImage(shop, "Shelf");
        // the line: smaller and darker, queued behind the one ordering at the counter
        var lineViews = new Image[2];
        for (int i = lineViews.Length - 1; i >= 0; i--) // furthest back drawn first
        {
            var spot = AddCustomerView(shop, "InLine_" + (i + 1), 240 + 18 * i);
            spot.localScale = new Vector3(0.75f, 0.75f, 1);
            lineViews[i] = spot.GetComponent<Image>();
            lineViews[i].enabled = false;
        }
        var looks = new[] { "Customer_Demo", "Customer_2", "Customer_3", "Customer_4" }
            .Select(l => AssetDatabase.LoadAssetAtPath<Sprite>(LayerDir + l + ".png")).ToArray();

        // pickup: up to 3 customers waiting for their drink, each with a ticket and patience bar.
        // The Ticket layer is drawn over spot 1; the others are copies shifted right.
        var ticketRect = rects["Ticket"];
        var pickupSpots = new CustomerManager.PickupSpot[3];
        for (int i = 0; i < pickupSpots.Length; i++)
        {
            float dx = 42 * i;
            var group = Group(shop, "Pickup_" + (i + 1));
            var canvasGroup = group.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;
            var view = AddCustomerView(group, "Customer", ticketRect.x + ticketRect.w / 2f + dx);
            view.localScale = new Vector3(0.85f, 0.85f, 1);
            var ticket = AddButton(group, "Ticket");
            ticket.anchoredPosition += new Vector2(dx, 0);
            var ticketText = AddText(ticket, "Text", 3, 3, 35, 20, "SUNDAY MATCHA", Px, Ink, TextAlignmentOptions.Center);
            Wrap(ticketText);
            ticketText.lineSpacing = LineGap(-1);
            AddRect(ticket, "PatienceBack", 3, 24, 35, 3, Ink);
            var patienceFill = AddRect(ticket, "PatienceFill", 3, 24, 35, 3, Hex("6fa04a"));
            pickupSpots[i] = new CustomerManager.PickupSpot
            {
                group = canvasGroup,
                view = view.GetComponent<Image>(),
                ticket = ticket.GetComponent<Button>(),
                ticketText = ticketText,
                patienceFill = patienceFill,
            };
        }
        var customer = AddCustomerView(shop, "Customer", 210.5f).gameObject.AddComponent<CanvasGroup>();
        AddImage(shop, "Shop_Counter");
        // click the tray to throw its snack away
        var tray = AddButton(shop, "Tray");
        var traySnack = AddSnackImage(tray, "TraySnack", 27, 2);
        traySnack.raycastTarget = true; // clicking the snack itself also hits the tray
        AddImage(shop, "Plant_Monstera");
        // the order bubble fades with the customer at the counter (patience lives on the tickets now)
        var orderUI = Group(shop, "OrderUI");
        var orderGroup = orderUI.gameObject.AddComponent<CanvasGroup>();
        orderGroup.blocksRaycasts = false;
        var bubble = AddImage(orderUI, "Order_Bubble");
        var order = AddText(bubble, "OrderText", 11, 3, 50, 41, "SUNDAY MATCHA\n<color=#8a2e2c>NO MILK</color>\n+ DANISH", Px, Ink, TextAlignmentOptions.Center);
        // orders vary in length: wrap, with lines packed 1px tighter than usual
        Wrap(order);
        order.lineSpacing = LineGap(-1);

        // guess bubbles for unclear orders: CustomerManager moves them over the customer being asked
        var guessPopup = new GameObject("GuessPopup", typeof(RectTransform)).GetComponent<RectTransform>();
        guessPopup.SetParent(shop, false);
        Place(guessPopup, 0, 58, 76, 46);
        var guessButtons = new Button[3];
        var guessLabels = new TMP_Text[3];
        for (int i = 0; i < guessButtons.Length; i++)
        {
            guessButtons[i] = AddRectButton(guessPopup, "Guess_" + (i + 1), 1, 1 + 15 * i, 74, 13, Color.white);
            var rt = (RectTransform)guessButtons[i].transform;
            AddText(rt, "Key", 3, 3, 5, 7, (i + 1).ToString(), Px, new Color(Ink.r, Ink.g, Ink.b, 0.45f), TextAlignmentOptions.TopLeft);
            guessLabels[i] = AddText(rt, "Label", 9, 0, 64, 13, "", Px, Ink, TextAlignmentOptions.Left);
        }
        guessPopup.gameObject.SetActive(false);

        // --- Drink station (top-right) ---
        var drink = Group(screen, "DrinkStation");
        AddImage(drink, "Drink_Station_Background");
        // placeholder labels for the editor; DrinkBrowser fills them from the drink tree at runtime
        string[] placeholder = { "DAIRY", "DRINK TYPE", "SWEETENERS", "MISC", "TOPPINGS" };
        string[] placeholderIcons = { "Dairy", "Drink Type", "Sweeteners", "Misc", "Toppings" };
        var slots = new Button[5];
        var slotLabels = new TMP_Text[5];
        var slotIcons = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var slot = AddButton(drink, "Slot_" + (i + 1));
            // icon and key number on the left, name on the right
            slotIcons[i] = AddRect(slot, "Icon", 3, 4, 8, 8, Color.white).GetComponent<Image>();
            slotIcons[i].sprite = Icon(placeholderIcons[i]);
            slotIcons[i].enabled = slotIcons[i].sprite != null;
            var label = AddText(slot, "Label", 12, 0, 44, 24, placeholder[i], Px, Ink, TextAlignmentOptions.Center);
            Wrap(label);
            slots[i] = slot.GetComponent<Button>();
            slotLabels[i] = label;
        }
        slots[4].interactable = false;
        var back = AddButton(drink, "Back_Button");
        AddText(back, "Label", 22, 0, 33, 24, "BACK", Px, MatchaDark, TextAlignmentOptions.Left);
        var path = AddImage(drink, "Path_Bar");
        var pathText = AddText(path, "PathText", 3, 0, 105, 11, "HOME", Px, Ink, TextAlignmentOptions.Left);
        var readout = AddText(drink, "CupReadout", 366, 48, 108, 33, "", Px, Ink, TextAlignmentOptions.TopLeft);
        Wrap(readout);
        var demoFill = AddImage(drink, "Cup_Fill_Demo");
        demoFill.gameObject.SetActive(false);
        // layers stack inside the glass shape: Cup_Mask is the glass interior
        var contents = AddImage(drink, "Cup_Mask", "CupContents");
        contents.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        AddImage(drink, "Cup");
        // DUMP starts the drink over, at a patience cost to everyone waiting
        var dump = AddButton(drink, "Dump_Button");
        var dumpRect = rects["Dump_Button"];
        AddText(drink, "DumpLabel", dumpRect.x - 3, 127, dumpRect.w + 6, 8, "DUMP", Px, Ink, TextAlignmentOptions.Center);
        var serve = AddButton(drink, "Serve_Button");

        var cup = contents.gameObject.AddComponent<Cup>();
        cup.contents = contents;
        cup.serveButton = serve.GetComponent<Button>();
        cup.dumpButton = dump.GetComponent<Button>();
        cup.readout = readout;
        cup.costMultiplier = balance.costMultiplier;
        cup.powderGrain = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Cup/Powder_Grain.png");
        cup.iceCube = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Cup/Ice_Cube.png");
        var iceFront = new GameObject("IceFront", typeof(RectTransform)).GetComponent<RectTransform>();
        iceFront.SetParent(contents, false);
        iceFront.anchorMin = Vector2.zero;
        iceFront.anchorMax = Vector2.one;
        iceFront.sizeDelta = Vector2.zero;
        cup.iceFront = iceFront;

        var browser = drink.gameObject.AddComponent<DrinkBrowser>();
        browser.root = AssetDatabase.LoadAssetAtPath<DrinkNode>(DrinkTreeImporter.RootAssetPath);
        if (browser.root == null) Debug.LogWarning("[Matcha] No drink tree yet: run Matcha > Import Drink Tree, then rebuild.");
        browser.slots = slots;
        browser.slotLabels = slotLabels;
        browser.slotIcons = slotIcons;
        browser.moreIcon = Icon("_More");
        if (browser.root != null)
        {
            AssignIcons(browser.root, Icon("_Default"));
            AssignLooks(browser.root, new List<string>());
            AssignCosts(browser.root);
            DrinkTreeImporter.MigrateRecipes();
            AssetDatabase.SaveAssets();
        }
        browser.backButton = back.GetComponent<Button>();
        browser.pathText = pathText;
        browser.cup = cup;
        AddText(drink, "ServeLabel", 450, 127, 28, 8, "SERVE", Px, Ink, TextAlignmentOptions.Center);

        // --- Prep station (bottom-left) ---
        var prep = Group(screen, "PrepStation");
        AddImage(prep, "Prep_Station_Background");
        Snack[] coldSnacks =
        {
            EnsureSnack("Matcha Roll", "Cold_Snack_1", false, 3, "Roll"),
            EnsureSnack("Strawberry Shortcake", "Cold_Snack_2", false, 4, "Cake", 2),
            EnsureSnack("Hanami Dango", "Cold_Snack_3", false, 3, "Dango"),
        };
        var danish = EnsureSnack("Danish", "Hot_Snack_1", true, 3, "Danish");
        var snackSlots = new List<SnackStation.Slot>();
        AddImage(prep, "Cold_Case");
        for (int i = 1; i <= 3; i++)
            snackSlots.Add(new SnackStation.Slot { button = AddButton(prep, "Cold_Snack_" + i).GetComponent<Button>(), snack = coldSnacks[i - 1] });
        AddImage(prep, "Hot_Case");
        for (int i = 1; i <= 3; i++)
            snackSlots.Add(new SnackStation.Slot { button = AddButton(prep, "Hot_Snack_" + i).GetComponent<Button>(), snack = danish });
        // microwave heats fast but burns fast; the toaster oven is slower and more forgiving
        var micro = AddButton(prep, "Microwave");
        var microSnack = AddSnackImage(micro, "Snack", 37, 35);
        var microTimer = AddText(micro, "TimerText", 4, 5, 66, 11, "", Px, Cream, TextAlignmentOptions.Center);
        var oven = AddButton(prep, "Toaster_Oven");
        var ovenSnack = AddSnackImage(oven, "Snack", 42, 26);
        var ovenTimer = AddText(oven, "TimerText", 6, 4, 72, 9, "", Px, Cream, TextAlignmentOptions.Center);

        var snacks = prep.gameObject.AddComponent<SnackStation>();
        snacks.slots = snackSlots.ToArray();
        snacks.appliances = new[]
        {
            new SnackStation.Appliance { name = "MICROWAVE", button = micro.GetComponent<Button>(), timerText = microTimer, snackImage = microSnack, readySeconds = 4, burnSeconds = 8 },
            new SnackStation.Appliance { name = "TOASTER OVEN", button = oven.GetComponent<Button>(), timerText = ovenTimer, snackImage = ovenSnack, readySeconds = 7, burnSeconds = 16 },
        };
        snacks.balance = balance;
        snacks.ApplyBalance();
        snacks.trayButton = tray.GetComponent<Button>();
        snacks.trayImage = traySnack;

        // --- Menu / recipe (bottom-right) ---
        var recipe = Group(screen, "RecipePanel");
        AddImage(recipe, "Recipe_Panel");
        var tabMenu = AddButton(recipe, "Tab_Menu");
        AddText(tabMenu, "Label", 0, 0, 36, 13, "MENU", Px, Ink, TextAlignmentOptions.Center);
        var tabRecipe = AddButton(recipe, "Tab_Recipe");
        AddText(tabRecipe, "Label", 0, 0, 46, 13, "RECIPE", Px, Ink, TextAlignmentOptions.Center);
        AddImage(recipe, "Recipe_Paper");
        var recipeTitle = AddText(recipe, "RecipeTitle", 318, 186, 128, 9, "SUNDAY MATCHA", Px, MatchaDark, TextAlignmentOptions.Left);
        // < > switch between waiting customers' recipes (arrow keys too)
        var prevCustomer = AddTextButton(recipe, "PrevCustomer", 452, 186, 8, 9, "<");
        var nextCustomer = AddTextButton(recipe, "NextCustomer", 462, 186, 8, 9, ">");
        recipeTitle.raycastTarget = true;
        var titleButton = recipeTitle.gameObject.AddComponent<Button>();
        titleButton.transition = Selectable.Transition.None;
        titleButton.navigation = new Navigation { mode = Navigation.Mode.None };
        var recipeBody = AddText(recipe, "RecipeBody", 316, 199, 156, 54,
            "[x] CEREMONIAL MATCHA 100G\n[ ] WATER 4 OZ\n[ ] ANY MILK 6 OZ\n[ ] ANY SWEETENER 15G\n[ ] HOT DANISH",
            Px, Ink, TextAlignmentOptions.TopLeft);
        Wrap(recipeBody);
        recipeBody.lineSpacing = LineGap(2); // 9px lines, one per ruled line on the paper

        // menu tab: two columns sitting on the paper's ruled lines (every 9px from y=200)
        var drinkHeader = AddText(recipe, "DrinkHeader", 316, 201, 76, 8, "DRINKS", Px, MatchaDark, TextAlignmentOptions.Left);
        var snackHeader = AddText(recipe, "SnackHeader", 396, 201, 76, 8, "SNACKS", Px, MatchaDark, TextAlignmentOptions.Left);
        var drinkRows = new Button[5];
        var snackRows = new Button[5];
        for (int i = 0; i < 5; i++)
        {
            drinkRows[i] = AddMenuRow(recipe, "Drink_" + (i + 1), 316, 210 + 9 * i);
            snackRows[i] = AddMenuRow(recipe, "Snack_" + (i + 1), 396, 210 + 9 * i);
        }
        var panel = recipe.gameObject.AddComponent<RecipePanel>();
        panel.menuTab = tabMenu.GetComponent<Button>();
        panel.recipeTab = tabRecipe.GetComponent<Button>();
        panel.title = recipeTitle;
        panel.titleButton = titleButton;
        panel.body = recipeBody;
        panel.prevCustomer = prevCustomer;
        panel.nextCustomer = nextCustomer;
        panel.drinkHeader = drinkHeader;
        panel.snackHeader = snackHeader;
        panel.drinkRows = drinkRows;
        panel.snackRows = snackRows;
        drinkHeader.gameObject.SetActive(false);
        snackHeader.gameObject.SetActive(false);

        // --- Status bar (bottom strip) ---
        var status = Group(screen, "StatusBar");
        AddImage(status, "Status_Bar");
        var coinsText = AddText(status, "CoinsText", 13, 259, 36, 10, "0", Px, Cream, TextAlignmentOptions.Left);
        var dayText = AddText(status, "DayText", 57, 259, 40, 10, "DAY 1", Px, Cream, TextAlignmentOptions.Left);
        var clockText = AddText(status, "ClockText", 113, 259, 60, 10, "9:00 AM", Px, Cream, TextAlignmentOptions.Left);
        var servedText = AddText(status, "ServedText", 340, 259, 76, 10, "SERVED 0", Px, Cream, TextAlignmentOptions.Right);
        // one lit dot per happy customer, over the 10 grey ones drawn in Status_Bar
        var servedDots = new GameObject[10];
        for (int i = 0; i < servedDots.Length; i++)
        {
            var dot = AddImage(status, "Served_Dot", "Served_Dot_" + (i + 1));
            dot.anchoredPosition += new Vector2(6 * i, 0);
            dot.gameObject.SetActive(false);
            servedDots[i] = dot.gameObject;
        }

        var customers = screen.gameObject.AddComponent<CustomerManager>();
        if (AssetDatabase.IsValidFolder(DrinkTreeImporter.RecipeDir))
            customers.recipes = AssetDatabase.FindAssets("t:DrinkRecipe", new[] { DrinkTreeImporter.RecipeDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<DrinkRecipe>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(r => r.name).ToList();
        if (customers.recipes.Count == 0) Debug.LogWarning("[Matcha] No recipes yet: run Matcha > Import Drink Tree, then rebuild.");
        customers.balance = balance;
        customers.drinkTree = browser.root;
        customers.cup = cup;
        customers.snacks = snacks;
        customers.customer = customer;
        customers.pickup = pickupSpots;
        customers.guessPopup = guessPopup;
        customers.guessButtons = guessButtons;
        customers.guessLabels = guessLabels;
        customers.lineViews = lineViews;
        customers.looks = looks;
        customers.orderUI = orderGroup;
        customers.orderText = order;
        customers.recipePanel = panel;
        panel.customers = customers;
        customers.coinsText = coinsText;
        customers.servedText = servedText;
        customers.servedDots = servedDots;

        AddImage(screen, "Frame");

        // --- End of day (on top of everything, hidden until closing) ---
        var summary = AddRect(screen, "DaySummary", 0, 0, RefWidth, RefHeight, new Color(0.06f, 0.05f, 0.1f, 0.7f));
        summary.GetComponent<Image>().raycastTarget = true; // blocks clicks on the shop behind it
        AddRect(summary, "Border", 150, 58, 180, 136, Ink);
        AddRect(summary, "Paper", 152, 60, 176, 132, Cream);
        var summaryTitle = AddText(summary, "Title", 152, 66, 176, 14, "DAY 1 DONE", Px2, MatchaDark, TextAlignmentOptions.Center);
        var summaryBody = AddText(summary, "Body", 170, 86, 140, 72, "", Px, Ink, TextAlignmentOptions.TopLeft);
        summaryBody.lineSpacing = LineGap(2);
        var nextDayButton = AddRectButton(summary, "NextDayButton", 190, 164, 100, 20, Hex("6fa04a"));
        AddText((RectTransform)nextDayButton.transform, "Label", 0, 0, 100, 20, "NEXT DAY", Px2, Cream, TextAlignmentOptions.Center);
        summary.gameObject.SetActive(false);

        var day = screen.gameObject.AddComponent<DayCycle>();
        day.balance = balance;
        day.difficulties = difficulties;
        day.customers = customers;
        day.cup = cup;
        day.snacks = snacks;
        day.dayText = dayText;
        day.clockText = clockText;
        day.summary = summary.gameObject;
        day.summaryTitle = summaryTitle;
        day.summaryBody = summaryBody;
        day.nextDayButton = nextDayButton;

        // --- Title screen (topmost; the day waits until CONTINUE or NEW GAME) ---
        var titleRoot = AddRect(screen, "TitleScreen", 0, 0, RefWidth, RefHeight, new Color(0.06f, 0.05f, 0.1f, 0.75f));
        titleRoot.GetComponent<Image>().raycastTarget = true;
        AddImage(titleRoot, "Title_Card");
        var mainPage = Group(titleRoot, "MainPage");
        var continueButton = AddRectButton(mainPage, "ContinueButton", 170, 150, 140, 28, Hex("6fa04a"));
        var continueRt = (RectTransform)continueButton.transform;
        AddText(continueRt, "Label", 0, 3, 140, 13, "CONTINUE", Px2, Cream, TextAlignmentOptions.Center);
        var continueInfo = AddText(continueRt, "Info", 0, 17, 140, 9, "DAY 1  -  0 COINS", Px, Cream, TextAlignmentOptions.Center);
        var newGameButton = AddRectButton(mainPage, "NewGameButton", 170, 186, 140, 20, Hex("e0d2b2"));
        var newGameLabel = AddText((RectTransform)newGameButton.transform, "Label", 0, 0, 140, 20, "NEW GAME", Px2, Ink, TextAlignmentOptions.Center);
        var title = titleRoot.gameObject.AddComponent<TitleScreen>();
        title.day = day;
        title.continueButton = continueButton;
        title.continueInfo = continueInfo;
        title.newGameButton = newGameButton;
        title.newGameLabel = newGameLabel;
        // difficulty page: name on the left, what it means on the right
        var difficultyPage = Group(titleRoot, "DifficultyPage");
        string[] difficultyColors = { "6fa04a", "c9a227", "b8423f" };
        var difficultyButtons = new Button[difficulties.Length];
        for (int i = 0; i < difficulties.Length; i++)
        {
            var b = AddRectButton(difficultyPage, "Difficulty_" + difficulties[i].difficultyName, 170, 148 + 19 * i, 140, 16, Hex(difficultyColors[i]));
            var rt = (RectTransform)b.transform;
            AddText(rt, "Name", 5, 1, 60, 14, difficulties[i].difficultyName, Px2, Cream, TextAlignmentOptions.Left);
            AddText(rt, "About", 50, 4, 86, 8, difficulties[i].description, Px, Cream, TextAlignmentOptions.Right);
            difficultyButtons[i] = b;
        }
        var backButton = AddTextButton(difficultyPage, "Back", 170, 207, 40, 9, "< BACK");
        difficultyPage.gameObject.SetActive(false);
        title.mainPage = mainPage.gameObject;
        title.difficultyPage = difficultyPage.gameObject;
        title.difficultyButtons = difficultyButtons;
        title.backButton = backButton;
        day.title = title;

        // --- Sound ---
        var sound = new GameObject("SoundFX").AddComponent<SoundFX>();
        sound.sounds = ((Sfx[])Enum.GetValues(typeof(Sfx))).Select(id => new SoundFX.Entry
        {
            id = id,
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{id}.wav"),
            volume = id == Sfx.Click ? 0.6f : 1f,
            pitchJitter = id == Sfx.Pour || id == Sfx.Click || id == Sfx.Tray ? 0.08f : 0f,
        }).ToArray();
        foreach (var e in sound.sounds.Where(e => e.clip == null))
            Debug.LogWarning($"[Matcha] Missing {AudioDir}/{e.id}.wav: run Tools/make_sfx.py");
        sound.clickButtons = new[]
        {
            back.GetComponent<Button>(), tabMenu.GetComponent<Button>(), tabRecipe.GetComponent<Button>(), titleButton,
            nextDayButton, continueButton, newGameButton,
        }.Concat(drinkRows).Concat(snackRows).ToArray();

        // --- Keyboard shortcuts + the key letters drawn on each button ---
        var keys = new GameObject("KeyboardShortcuts").AddComponent<KeyboardShortcuts>();
        var hints = new List<GameObject>();
        var dimInk = new Color(Ink.r, Ink.g, Ink.b, 0.5f);
        var dimCream = new Color(Cream.r, Cream.g, Cream.b, 0.7f);
        void Hint(RectTransform parent, float x, float y, string key, Color color) =>
            hints.Add(AddText(parent, "KeyHint", x, y, key.Length * 4 + 2, 7, key, Px, color, TextAlignmentOptions.TopLeft).gameObject);
        // right-aligned against a parent's right edge
        void HintRight(RectTransform parent, float right, float y, string key, Color color) =>
            Hint(parent, right - (key.Length * 4 - 1), y, key, color);

        var bindings = new List<KeyboardShortcuts.Binding>();
        void Bind(Key key, Key alt, RectTransform button, Key alt2 = Key.None) =>
            bindings.Add(new KeyboardShortcuts.Binding { key = key, altKey = alt, altKey2 = alt2, button = button.GetComponent<Button>() });

        for (int i = 0; i < slots.Length; i++)
        {
            Bind(Key.Digit1 + i, Key.Numpad1 + i, (RectTransform)slots[i].transform);
            Hint((RectTransform)slots[i].transform, 5, 13, (i + 1).ToString(), dimInk); // under the icon
        }
        Bind(Key.Backspace, Key.Escape, back, Key.P);
        HintRight(back, rects["Back_Button"].w - 4, 2, "ESC/P", dimInk);
        Bind(Key.Space, Key.Enter, serve);
        var serveRect = rects["Serve_Button"];
        Hint(drink, serveRect.x + serveRect.w / 2f - 10, 119, "SPACE", dimInk); // just above the SERVE label
        Bind(Key.X, Key.None, dump);
        Hint(drink, dumpRect.x + dumpRect.w / 2f - 1, 119, "X", dimInk); // above the DUMP label
        Hint(drink, 366, 96, "Z=AGAIN", dimInk); // left of the glass: pour the last ingredient again

        Key[] snackKeys = { Key.Q, Key.W, Key.E, Key.A, Key.S, Key.D };
        for (int i = 0; i < snackSlots.Count; i++)
        {
            var rt = (RectTransform)snackSlots[i].button.transform;
            Bind(snackKeys[i], Key.None, rt);
            var r = rects[rt.name];
            Hint(prep, r.x - 8, r.y - 2, snackKeys[i].ToString(), i < 3 ? dimInk : dimCream);
        }
        Bind(Key.R, Key.None, micro);
        Hint(micro, 6, 6, "R", dimCream);
        Bind(Key.F, Key.None, oven);
        Hint(oven, 8, 6, "F", dimCream);
        Bind(Key.T, Key.None, tray);
        var trayRect = rects["Tray"];
        Hint(shop, trayRect.x - 6, trayRect.y - 3, "T", dimCream);
        Hint(recipe, 393, 174, "TAB", dimCream);
        Hint(recipe, 411, 174, "SHIFT+1-9", dimCream); // jump to a checklist line (the grey numbers)
        HintRight(continueRt, 137, 2, "ENTER", dimCream);
        HintRight((RectTransform)newGameButton.transform, 137, 2, "N", dimInk);
        HintRight((RectTransform)nextDayButton.transform, 97, 2, "ENTER", dimCream);

        keys.game = bindings.ToArray();
        keys.recipePanel = panel;
        keys.customers = customers;
        keys.browser = browser;
        keys.title = title;
        keys.continueButton = continueButton;
        keys.newGameButton = newGameButton;
        keys.summary = summary.gameObject;
        keys.nextDayButton = nextDayButton;
        keys.hints = hints.ToArray();
        var preview = AddImage(screen, "Text_Preview");
        preview.gameObject.SetActive(false); // reference only: real text is TextMeshPro

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.productName = "Matcha Manager";
        Debug.Log("[Matcha] Built " + ScenePath);
    }

    // Renders the scene to a 1920x1080 PNG (used to check the layout from the command line).
    public static void CapturePreview()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Capture(Arg("-captureOut") ?? "Preview.png");
    }

    public static void Capture(string outPath)
    {
        var cam = Camera.main;
        var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        var rt = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = rt;
        canvas.GetComponent<CanvasScaler>().enabled = false;
        canvas.scaleFactor = 4;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Debug.Log("[Matcha] Wrote " + outPath);
    }

    // ---------------------------------------------------------------------

    static void LoadRects()
    {
        var json = File.ReadAllText(LayerDir + "layers.json");
        var data = JsonUtility.FromJson<LayerData>(json);
        rects = new Dictionary<string, LayerRect>();
        foreach (var f in data.frames) rects[f.filename] = f.spriteSourceSize;
    }

    static RectTransform Group(RectTransform parent, string name)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Place(rt, 0, 0, RefWidth, RefHeight);
        return rt;
    }

    // Top-left anchored placement using Aseprite pixel coordinates.
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static RectTransform AddImage(RectTransform parent, string layer, string objectName = null)
    {
        if (!rects.TryGetValue(layer, out var r)) throw new Exception("Missing layer in layers.json: " + layer);
        var go = new GameObject(objectName ?? layer, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        // groups sit at the screen origin, so absolute coords work for every child
        Place(rt, r.x, r.y, r.w, r.h);
        var img = go.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LayerDir + layer + ".png");
        img.raycastTarget = false;
        return rt;
    }

    // A customer image pivoted at the feet (bottom-center at x, standing behind the counter),
    // so looks of different heights all stand on the same spot.
    static RectTransform AddCustomerView(RectTransform parent, string name, float x)
    {
        var rt = AddImage(parent, "Customer_Demo", name);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(x, -144);
        return rt;
    }

    // Solid-color button with a 1px ink border, for screens that have no button art.
    static Button AddRectButton(RectTransform parent, string name, float x, float y, float w, float h, Color color)
    {
        AddRect(parent, name + "Border", x - 1, y - 1, w + 2, h + 2, Ink);
        var rt = AddRect(parent, name, x, y, w, h, color);
        rt.GetComponent<Image>().raycastTarget = true;
        var btn = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
        colors.disabledColor = new Color(0.7f, 0.72f, 0.74f, 0.55f);
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        return btn;
    }

    // Icon sprite for a drink-tree name ("Oat Milk" -> Assets/Art/Icons/Oat Milk.png), null if none.
    static Sprite Icon(string name) =>
        string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{name}.png");

    // Gives every node without an icon its own (by name) or else its folder's, so new
    // vault entries still get a sensible picture. Icons picked in the Inspector are kept.
    static void AssignIcons(DrinkNode node, Sprite inherited)
    {
        if (node.icon == null)
        {
            node.icon = Icon(node.displayName) ?? inherited;
            EditorUtility.SetDirty(node);
        }
        foreach (var child in node.children) AssignIcons(child, node.icon);
    }

    // Default cost per portion, where the tree has a cheap and a premium choice.
    // Applied once per ingredient (economyVersion); after that the Inspector values are yours.
    static readonly Dictionary<string, int> Economy = new Dictionary<string, int>
    {
        { "Whole Milk", 1 }, { "Half and Half", 1 }, { "Heavy Cream", 2 },
        { "Oat Milk", 2 }, { "Almond Milk", 2 }, { "Soy Milk", 1 }, { "Pistachio Milk", 3 },
        { "Ceremonial Matcha", 3 }, { "Low-Grade Matcha", 1 },
        { "Hojicha", 2 }, { "Jasmine", 1 }, { "Earl Gray", 1 },
        { "Dark Chocolate", 2 }, { "Milk Chocolate", 1 }, { "White Chocolate", 1 },
        { "Arabrica", 2 }, { "Robusta", 1 }, { "Decaf", 1 }, { "Cold Brew", 2 },
        { "Cane Sugar", 1 }, { "White Sugar", 0 }, { "Brown Sugar", 1 }, { "Stevia", 1 },
        { "Maple Syrup", 2 }, { "Vanilla Syrup", 1 }, { "Ube Syrup", 2 },
        { "Water", 0 }, { "Hot Water", 0 }, { "Cold Water", 0 }, { "Sparkling Water", 1 }, { "Ice", 0 },
        { "Coconut Flakes", 1 }, { "Caramel Honeycomb", 2 }, { "Caramel Drizzle", 1 }, { "Biscoff Crumbles", 1 },
        { "Cheese Foam", 2 }, { "Sesame Foam", 2 }, { "Matcha Foam", 2 },
    };

    static void AssignCosts(DrinkNode node)
    {
        if (node.IsIngredient && node.economyVersion < 1)
        {
            if (Economy.TryGetValue(node.displayName, out var cost)) node.cost = cost;
            node.economyVersion = 1;
            EditorUtility.SetDirty(node);
        }
        foreach (var child in node.children) AssignCosts(child);
    }

    // Resolves CupLook.Auto from the tree: Ice is ice, sugars, matcha and crumbly garnishes
    // are powders, the rest (syrups, foams, drizzle) are liquids. Looks picked in the Inspector are kept.
    static void AssignLooks(DrinkNode node, List<string> ancestors)
    {
        if (node.IsIngredient && node.look == CupLook.Auto)
        {
            node.look = node.displayName == "Ice" ? CupLook.Ice
                : node.displayName == "Caramel Drizzle" ? CupLook.Liquid
                : ancestors.Contains("Sugar") || ancestors.Contains("Matcha") || ancestors.Contains("Garnish") ? CupLook.Powder
                : CupLook.Liquid;
            EditorUtility.SetDirty(node);
        }
        ancestors.Add(node.displayName);
        foreach (var child in node.children) AssignLooks(child, ancestors);
        ancestors.RemoveAt(ancestors.Count - 1);
    }

    // Plain colored rectangle (no sprite), in its parent's top-left pixel coords.
    static RectTransform AddRect(RectTransform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Place(rt, x, y, w, h);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // Clickable text: ink normally, green on hover (like the menu rows, without a price).
    static Button AddTextButton(RectTransform parent, string name, float x, float y, float w, float h, string text)
    {
        var label = AddText(parent, name, x, y, w, h, text, Px, Color.white, TextAlignmentOptions.Left);
        label.raycastTarget = true;
        var btn = label.gameObject.AddComponent<Button>();
        btn.targetGraphic = label;
        var colors = btn.colors;
        colors.normalColor = Ink;
        colors.highlightedColor = Hex("3f6b2e");
        colors.pressedColor = MatchaDark;
        colors.selectedColor = Ink;
        colors.disabledColor = new Color(Ink.r, Ink.g, Ink.b, 0.25f);
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        return btn;
    }

    // A clickable line of text on the menu, with its price in a right-hand column.
    // The text itself is the button's graphic: ink normally, green on hover.
    static Button AddMenuRow(RectTransform parent, string name, float x, float y)
    {
        var label = AddText(parent, name, x, y, 76, 8, "", Px, Color.white, TextAlignmentOptions.Left);
        label.margin = new Vector4(0, 0, 12, 0);
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = true;
        var price = AddText(label.rectTransform, "Price", 64, 0, 12, 8, "", Px, MatchaDark, TextAlignmentOptions.Right);
        var btn = label.gameObject.AddComponent<Button>();
        btn.targetGraphic = label;
        var colors = btn.colors;
        colors.normalColor = Ink;
        colors.highlightedColor = Hex("3f6b2e");
        colors.pressedColor = MatchaDark;
        colors.selectedColor = Ink;
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        label.gameObject.SetActive(false);
        return btn;
    }

    // Empty image whose bottom-center sits at (x, bottom) in its parent; SnackStation fills in the sprite.
    static Image AddSnackImage(RectTransform parent, string name, float x, float bottom)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(x, -bottom);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    // Medium keeps the original Balance.asset path, so tuning done before difficulties carries over.
    public static readonly string[] DifficultyPaths =
        { "Assets/Data/Balance Easy.asset", "Assets/Data/Balance.asset", "Assets/Data/Balance Hard.asset" };

    // One Balance asset per difficulty; each is created once with these presets, then it's yours to tune.
    public static Balance[] EnsureDifficulties()
    {
        var presets = new System.Action<Balance>[]
        {
            b =>
            {
                b.difficultyName = "EASY"; b.description = "PATIENT, SIMPLE ORDERS";
                b.arrivalInterval = new Vector2(18, 28); b.linePatienceRate = 0.3f;
                b.snackChance = 0.3f; b.changeChance = 0.3f; b.changeChancePerDay = 0.02f; b.maxChangeChance = 0.5f; b.maxChanges = 1;
                b.addChance = 0; b.doubleExtraChance = 0; b.tipMultiplier = 1.2f;
                b.harderPerDay = 0.05f; b.hardestAt = 0.6f; b.dailyGoal = 6; b.goalPerDay = 1; b.maxGoal = 16;
                b.microwaveBurn = 10; b.ovenBurn = 20;
            },
            b => { }, // medium: the Balance defaults
            b =>
            {
                b.difficultyName = "HARD"; b.description = "PICKY AND IN A HURRY";
                b.arrivalInterval = new Vector2(11, 19); b.linePatienceRate = 0.5f;
                b.snackChance = 0.5f; b.changeChance = 0.7f; b.changeChancePerDay = 0.05f; b.maxChangeChance = 0.95f; b.maxChanges = 4;
                b.addChance = 0.35f; b.doubleExtraChance = 0.3f; b.tipMultiplier = 0.8f;
                b.harderPerDay = 0.06f; b.hardestAt = 0.45f; b.dailyGoal = 10; b.goalPerDay = 2; b.maxGoal = 30;
                b.microwaveBurn = 7; b.ovenBurn = 13;
            },
        };
        // settings added after the first presets; applied once to assets made before them
        var presetsV1 = new System.Action<Balance>[]
        {
            b => { b.unclearChance = 0; b.unclearChancePerDay = 0; b.maxUnclearChance = 0; }, // never on easy
            b => { b.unclearChance = 0.15f; b.unclearChancePerDay = 0.02f; b.maxUnclearChance = 0.35f; },
            b => { b.unclearChance = 0.3f; b.unclearChancePerDay = 0.03f; b.maxUnclearChance = 0.6f; },
        };
        // cost to operate: cheaper on easy, pricier on hard
        var presetsV2 = new System.Action<Balance>[]
        {
            b => { b.startingCoins = 30; b.costMultiplier = 0.7f; },
            b => { },
            b => { b.startingCoins = 15; b.costMultiplier = 1.2f; },
        };
        // patience from key presses, fast bonus, late pay cut: roomier on easy, tighter on hard
        var presetsV3 = new System.Action<Balance>[]
        {
            b => { b.patienceBase = 14; b.secondsPerPress = 5.5f; b.fastZone = 0.55f; b.fastBonus = 0.5f; b.lateZone = 0.2f; b.latePay = 0.75f; },
            b => { },
            b => { b.patienceBase = 8; b.secondsPerPress = 2.8f; b.fastZone = 0.75f; b.fastBonus = 0.6f; b.lateZone = 0.35f; b.latePay = 0.4f; },
        };
        // mumble peek: fewer clear letters, sliding faster on hard
        var presetsV4 = new System.Action<Balance>[]
        {
            b => { },
            b => { },
            b => { b.peekLetters = 2; b.peekSpeed = 4.5f; b.peekPause = 1; },
        };
        var result = new Balance[DifficultyPaths.Length];
        for (int i = 0; i < DifficultyPaths.Length; i++)
        {
            result[i] = AssetDatabase.LoadAssetAtPath<Balance>(DifficultyPaths[i]);
            if (result[i] == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DifficultyPaths[i]));
                result[i] = ScriptableObject.CreateInstance<Balance>();
                presets[i](result[i]);
                AssetDatabase.CreateAsset(result[i], DifficultyPaths[i]);
            }
            if (result[i].presetVersion < 1)
            {
                presetsV1[i](result[i]);
                result[i].presetVersion = 1;
                EditorUtility.SetDirty(result[i]);
            }
            if (result[i].presetVersion < 2)
            {
                presetsV2[i](result[i]);
                result[i].presetVersion = 2;
                EditorUtility.SetDirty(result[i]);
            }
            if (result[i].presetVersion < 3)
            {
                presetsV3[i](result[i]);
                result[i].presetVersion = 3;
                EditorUtility.SetDirty(result[i]);
            }
            if (result[i].presetVersion < 4)
            {
                presetsV4[i](result[i]);
                result[i].presetVersion = 4;
                EditorUtility.SetDirty(result[i]);
            }
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    // Creates a snack asset the first time; after that it keeps whatever was edited in the Inspector.
    static Snack EnsureSnack(string name, string layer, bool hot, int price, string ticketName, int cost = 1)
    {
        var path = $"{SnackDir}/{name}.asset";
        var snack = AssetDatabase.LoadAssetAtPath<Snack>(path);
        if (snack != null)
        {
            // snacks made before tickets existed get a short name too
            if (string.IsNullOrEmpty(snack.ticketName))
            {
                snack.ticketName = ticketName;
                EditorUtility.SetDirty(snack);
            }
            return snack;
        }
        Directory.CreateDirectory(SnackDir);
        snack = ScriptableObject.CreateInstance<Snack>();
        snack.displayName = name;
        snack.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LayerDir + layer + ".png");
        snack.needsHeating = hot;
        snack.ticketName = ticketName;
        snack.price = price;
        snack.cost = cost;
        AssetDatabase.CreateAsset(snack, path);
        return snack;
    }

    static RectTransform AddButton(RectTransform parent, string layer)
    {
        var rt = AddImage(parent, layer);
        rt.GetComponent<Image>().raycastTarget = true;
        var btn = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.7f, 0.72f, 0.74f, 0.55f);
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        return rt;
    }

    // Text positioned relative to its parent's top-left corner (groups sit at the screen origin).
    static TextMeshProUGUI AddText(RectTransform parent, string name, float x, float y, float w, float h,
        string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Place(rt, x, y, w, h);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = pixelFont;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Wrap long text inside its box and cut off whatever still doesn't fit. No auto-sizing:
    // in-between sizes would draw the pixel font's pixels unevenly.
    static void Wrap(TMP_Text tmp)
    {
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
    }

    // TMP line spacing for an extra (or, negative, fewer) number of pixels between lines.
    static float LineGap(int pixels) => pixels * 100f / Px;

    public static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
