using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
        // the line: further back (smaller, a little darker), left of the tray so snacks stay visible
        var lineViews = new Image[2];
        for (int i = 0; i < lineViews.Length; i++)
        {
            var spot = AddCustomerView(shop, "InLine_" + (i + 1), 115 - 43 * i);
            spot.localScale = new Vector3(0.9f, 0.9f, 1);
            lineViews[i] = spot.GetComponent<Image>();
            lineViews[i].enabled = false;
        }
        var customer = AddCustomerView(shop, "Customer", 210.5f).gameObject.AddComponent<CanvasGroup>();
        var looks = new[] { "Customer_Demo", "Customer_2", "Customer_3", "Customer_4" }
            .Select(l => AssetDatabase.LoadAssetAtPath<Sprite>(LayerDir + l + ".png")).ToArray();
        AddImage(shop, "Shop_Counter");
        // click the tray to throw its snack away
        var tray = AddButton(shop, "Tray");
        var traySnack = AddSnackImage(tray, "TraySnack", 27, 2);
        traySnack.raycastTarget = true; // clicking the snack itself also hits the tray
        AddImage(shop, "Plant_Monstera");
        // bubble + patience bar fade together with the customer
        var orderUI = Group(shop, "OrderUI");
        var orderGroup = orderUI.gameObject.AddComponent<CanvasGroup>();
        orderGroup.blocksRaycasts = false;
        var bubble = AddImage(orderUI, "Order_Bubble");
        var order = AddText(bubble, "OrderText", 11, 3, 50, 41, "SUNDAY MATCHA\n<color=#8a2e2c>NO MILK</color>\n+ DANISH", Px, Ink, TextAlignmentOptions.Center);
        // orders vary in length: wrap, with lines packed 1px tighter than usual
        Wrap(order);
        order.lineSpacing = LineGap(-1);
        AddImage(orderUI, "Patience_Track");
        var fill = AddImage(orderUI, "Patience_Fill").GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0.7f;

        // --- Drink station (top-right) ---
        var drink = Group(screen, "DrinkStation");
        AddImage(drink, "Drink_Station_Background");
        // placeholder labels for the editor; DrinkBrowser fills them from the drink tree at runtime
        string[] placeholder = { "DAIRY", "FLAVORS", "SWEETENERS", "MISC", "" };
        var slots = new Button[5];
        var slotLabels = new TMP_Text[5];
        for (int i = 0; i < 5; i++)
        {
            var slot = AddButton(drink, "Slot_" + (i + 1));
            var label = AddText(slot, "Label", 2, 0, 53, 24, placeholder[i], Px, Ink, TextAlignmentOptions.Center);
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
        // clicking the glass pours the drink out
        var glass = AddButton(drink, "Cup");
        var serve = AddButton(drink, "Serve_Button");

        var cup = contents.gameObject.AddComponent<Cup>();
        cup.contents = contents;
        cup.serveButton = serve.GetComponent<Button>();
        cup.dumpButton = glass.GetComponent<Button>();
        cup.readout = readout;

        var browser = drink.gameObject.AddComponent<DrinkBrowser>();
        browser.root = AssetDatabase.LoadAssetAtPath<DrinkNode>(DrinkTreeImporter.RootAssetPath);
        if (browser.root == null) Debug.LogWarning("[Matcha] No drink tree yet: run Matcha > Import Drink Tree, then rebuild.");
        browser.slots = slots;
        browser.slotLabels = slotLabels;
        browser.backButton = back.GetComponent<Button>();
        browser.pathText = pathText;
        browser.cup = cup;
        AddText(drink, "ServeLabel", 450, 127, 28, 8, "SERVE", Px, Ink, TextAlignmentOptions.Center);

        // --- Prep station (bottom-left) ---
        var prep = Group(screen, "PrepStation");
        AddImage(prep, "Prep_Station_Background");
        Snack[] coldSnacks =
        {
            EnsureSnack("Matcha Roll", "Cold_Snack_1", false, 3),
            EnsureSnack("Strawberry Shortcake", "Cold_Snack_2", false, 4),
            EnsureSnack("Hanami Dango", "Cold_Snack_3", false, 3),
        };
        var danish = EnsureSnack("Danish", "Hot_Snack_1", true, 3);
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
        var recipeTitle = AddText(recipe, "RecipeTitle", 318, 186, 150, 9, "SUNDAY MATCHA", Px, MatchaDark, TextAlignmentOptions.Left);
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
        customers.cup = cup;
        customers.snacks = snacks;
        customers.customer = customer;
        customers.lineViews = lineViews;
        customers.looks = looks;
        customers.orderUI = orderGroup;
        customers.orderText = order;
        customers.patienceFill = fill;
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
        var continueButton = AddRectButton(titleRoot, "ContinueButton", 170, 150, 140, 28, Hex("6fa04a"));
        var continueRt = (RectTransform)continueButton.transform;
        AddText(continueRt, "Label", 0, 3, 140, 13, "CONTINUE", Px2, Cream, TextAlignmentOptions.Center);
        var continueInfo = AddText(continueRt, "Info", 0, 17, 140, 9, "DAY 1  -  0 COINS", Px, Cream, TextAlignmentOptions.Center);
        var newGameButton = AddRectButton(titleRoot, "NewGameButton", 170, 186, 140, 20, Hex("e0d2b2"));
        var newGameLabel = AddText((RectTransform)newGameButton.transform, "Label", 0, 0, 140, 20, "NEW GAME", Px2, Ink, TextAlignmentOptions.Center);
        var title = titleRoot.gameObject.AddComponent<TitleScreen>();
        title.day = day;
        title.continueButton = continueButton;
        title.continueInfo = continueInfo;
        title.newGameButton = newGameButton;
        title.newGameLabel = newGameLabel;
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

    // Creates a snack asset the first time; after that it keeps whatever was edited in the Inspector.
    static Snack EnsureSnack(string name, string layer, bool hot, int price)
    {
        var path = $"{SnackDir}/{name}.asset";
        var snack = AssetDatabase.LoadAssetAtPath<Snack>(path);
        if (snack != null) return snack;
        Directory.CreateDirectory(SnackDir);
        snack = ScriptableObject.CreateInstance<Snack>();
        snack.displayName = name;
        snack.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LayerDir + layer + ".png");
        snack.needsHeating = hot;
        snack.price = price;
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
