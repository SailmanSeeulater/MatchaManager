using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for customers and orders: serves a correct drink, a wrong one,
// lets one customer time out, heats danishes (cold / warm / burnt), and checks
// that random orders can be made correctly.
// Run: Unity -batchmode -quit -projectPath . -executeMethod CustomerCheck.Run -captureOut out.png
public static class CustomerCheck
{
    static CustomerManager customers;
    static DrinkBrowser browser;
    static Cup cup;
    static SnackStation snacks;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        customers = Object.FindAnyObjectByType<CustomerManager>();
        browser = Object.FindAnyObjectByType<DrinkBrowser>();
        cup = customers.cup;
        snacks = customers.snacks;
        Random.InitState(7);
        customers.Init();
        browser.ShowRoot();
        Debug.Log($"[Check] recipes: {string.Join(", ", customers.recipes.Select(r => r.displayName))}");
        Log("start");

        // 1. plain Sunday Matcha, made correctly
        var sunday = customers.recipes.First(r => r.displayName == "Sunday Matcha");
        customers.Arrive(new Order(sunday));
        customers.Tick(1);
        Log("arrived");
        Make("Ceremonial Matcha", "Water", "Oat Milk");
        Log("almost made");
        Make("Cane Sugar");
        Log("made");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "CustomerCheck.png");
        cup.Serve();
        Log("served correct");
        customers.Tick(5); customers.Tick(5); customers.Tick(5);
        Log("after leaving");

        // 2. wrong drink: extra milk nobody asked for
        customers.Arrive(new Order(sunday));
        customers.Tick(1);
        Make("Ceremonial Matcha", "Water", "Oat Milk", "Whole Milk", "Stevia");
        Log("wrong drink");
        cup.Serve();
        Log("served wrong");
        cup.Clear();
        customers.Tick(5); customers.Tick(5);

        // 3. too slow
        customers.Arrive(new Order(sunday));
        customers.Tick(1);
        customers.Tick(30);
        Log("waited 30s");
        customers.Tick(20);
        Log("waited 50s");
        customers.Tick(5); customers.Tick(5);

        // 4. snacks: a cold one straight onto the tray, then danishes taken out too early, on time, too late
        Snack Find(string name) => snacks.Menu.First(s => s.displayName == name);
        var roll = Find("Matcha Roll");
        var danish = Find("Danish");
        ServeWithSnack(new Order(sunday, roll), () => snacks.ClickSlot(0), "cold roll");
        ServeWithSnack(new Order(sunday, danish), () => { snacks.ClickSlot(3); snacks.ClickAppliance(0); snacks.Tick(2); snacks.ClickAppliance(0); }, "danish 2s microwave");
        ServeWithSnack(new Order(sunday, danish), () =>
        {
            snacks.ClickSlot(4);
            Debug.Log($"[Check] holding {snacks.Held?.displayName}; oven says '{snacks.appliances[1].timerText.text}'");
            snacks.ClickAppliance(1); snacks.Tick(3);
            Debug.Log($"[Check] oven after 3s: '{snacks.appliances[1].timerText.text}'");
            snacks.Tick(5);
            Debug.Log($"[Check] oven after 8s: '{snacks.appliances[1].timerText.text}'");
            MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut")?.Replace(".png", "_snack.png") ?? "SnackCheck.png");
            snacks.ClickAppliance(1);
        }, "danish 8s oven");
        ServeWithSnack(new Order(sunday, danish), () => { snacks.ClickSlot(5); snacks.ClickAppliance(0); snacks.Tick(9); snacks.ClickAppliance(0); }, "danish 9s microwave");
        ServeWithSnack(new Order(sunday, danish), () => snacks.ClickSlot(0), "roll for a danish");
        ServeWithSnack(new Order(sunday, danish), () => { }, "no snack");

        // 5. menu tab: list, a drink's recipe, a snack, back, and a new customer switching to RECIPE
        var panel = customers.recipePanel;
        panel.ShowTab(RecipePanel.Tab.Menu);
        LogMenu("menu list");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut")?.Replace(".png", "_menu.png") ?? "MenuCheck.png");
        panel.ClickDrink(customers.recipes.FindIndex(r => r.displayName == "Sunday Matcha"));
        LogMenu("menu sunday");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut")?.Replace(".png", "_detail.png") ?? "MenuDetail.png");
        panel.BackToMenu();
        panel.ClickSnack(System.Array.IndexOf(snacks.Menu, danish));
        LogMenu("menu danish");
        panel.BackToMenu();
        LogMenu("back");
        customers.Arrive(new Order(sunday));
        customers.Tick(1);
        LogMenu("customer came");
        customers.Tick(60); customers.Tick(5); customers.Tick(5);

        // 6. random orders: the drink that matches each order must pass Check
        int bad = 0;
        for (int i = 0; i < 40; i++)
        {
            var recipe = customers.recipes[i % customers.recipes.Count];
            var order = Order.Random(recipe, snacks.Menu);
            var drink = new Dictionary<DrinkNode, int>();
            foreach (var line in order.lines.Where(l => !l.removed))
                drink[line.node.Ingredients().First()] = line.portions;
            bool ok = order.Check(drink).Correct;
            if (!ok) bad++;
            if (i < 12 || !ok)
                Debug.Log($"[Check] order {recipe.displayName} [{string.Join(" | ", order.requests)}] snack={order.snack?.displayName} correct-drink passes={ok}");
        }
        Debug.Log($"[Check] random orders failing their own correct drink: {bad}/40");
    }

    static void ServeWithSnack(Order order, System.Action prepSnack, string what)
    {
        customers.Arrive(order);
        customers.Tick(1);
        Make("Ceremonial Matcha", "Water", "Oat Milk", "Cane Sugar");
        prepSnack();
        Log("tray: " + what);
        int before = customers.Coins;
        cup.Serve();
        Debug.Log($"[Check] {what,-20} -> '{customers.orderText.text.Replace("\n", " ")}' paid={customers.Coins - before} trayAfter={(snacks.TraySnack != null ? snacks.TraySnack.displayName : "empty")}");
        customers.Tick(5); customers.Tick(5);
    }

    static void LogMenu(string step)
    {
        var p = customers.recipePanel;
        string Rows(UnityEngine.UI.Button[] rows) => string.Join(" | ", rows.Where(r => r.gameObject.activeSelf)
            .Select(r => ((TMPro.TMP_Text)r.targetGraphic).text + " " + r.transform.Find("Price").GetComponent<TMPro.TMP_Text>().text));
        Debug.Log($"[Check] {step,-14} tab={p.Current} title='{p.title.text}' body='{p.body.text.Replace("\n", " / ")}' " +
                  $"drinks=[{Rows(p.drinkRows)}] snacks=[{Rows(p.snackRows)}]");
    }

    static void Make(params string[] ingredients)
    {
        foreach (var name in ingredients)
        {
            var node = browser.root.Ingredients().First(n => n.displayName == name);
            cup.Add(node);
        }
    }

    static void Log(string step)
    {
        Debug.Log($"[Check] {step,-16} order='{customers.orderText.text.Replace("\n", " / ")}' " +
                  $"patience={customers.patienceFill.fillAmount:0.00} serve={(cup.serveButton.interactable ? "on" : "off")} " +
                  $"coins={customers.coinsText.text} {customers.servedText.text}\n" +
                  $"        recipe: {customers.recipePanel.title.text} :: {customers.recipePanel.body.text.Replace("\n", " / ")}");
    }
}
