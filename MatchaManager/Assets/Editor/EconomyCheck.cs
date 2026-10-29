using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for cost to operate: default costs, slot labels, coins spent as
// ingredients are poured (and lost on a dump), snack costs, cheap vs premium
// drinks (same pay, so cheap is more profit), and WITH orders never naming the
// cheapest choice.
// Run: Unity -batchmode -quit -projectPath . -executeMethod EconomyCheck.Run -captureOut out.png
public static class EconomyCheck
{
    static CustomerManager customers;
    static DrinkBrowser browser;
    static Cup cup;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Object.FindAnyObjectByType<TitleScreen>().gameObject.SetActive(false);
        customers = Object.FindAnyObjectByType<CustomerManager>();
        browser = Object.FindAnyObjectByType<DrinkBrowser>();
        cup = customers.cup;
        var snacks = customers.snacks;
        Random.InitState(2);
        customers.Init();
        customers.autoArrive = false;
        browser.ShowRoot();

        foreach (var n in browser.root.Ingredients())
            Debug.Log($"[Check] {n.displayName,-18} cost ${n.cost}");
        var sunday = customers.recipes.First(r => r.displayName == "Sunday Matcha");
        Debug.Log($"[Check] Sunday Matcha lines: {string.Join(", ", new Order(sunday).lines.Select(l => l.Name))}");

        // the matcha folder's buttons show their cost
        foreach (var name in new[] { "Drink Type", "Tea", "Matcha" })
            browser.ClickSlot(browser.VisibleEntries().FindIndex(n => n.displayName == name));
        Debug.Log($"[Check] matcha slots: {string.Join(" | ", browser.slotLabels.Take(2).Select(l => l.text.Replace("\n", " ")))}");
        browser.ShowRoot();

        // spending as you pour; a dump doesn't refund
        int coins = customers.Coins;
        Add("Ceremonial Matcha", "Water", "Pistachio Milk");
        Debug.Log($"[Check] poured premium: coins {coins} -> {customers.Coins}, readout '{cup.readout.text.Replace("\n", " / ")}'");
        cup.PourOut();
        Debug.Log($"[Check] after dump: coins {customers.Coins} (spent today {customers.Today.spent})");
        coins = customers.Coins;
        snacks.ClickSlot(1); // strawberry shortcake
        Debug.Log($"[Check] cake onto the tray: coins {coins} -> {customers.Coins}");
        snacks.ClearTray();

        // cheap vs premium: customers pay the same, so the cheap cup keeps more
        var premium = new[] { "Ceremonial Matcha", "Hot Water", "Pistachio Milk", "Maple Syrup" };
        var middle = new[] { "Ceremonial Matcha", "Hot Water", "Whole Milk", "Cane Sugar" };
        var cheap = new[] { "Low-Grade Matcha", "Hot Water", "Soy Milk", "White Sugar" };
        foreach (var (label, drink) in new[] { ("premium", premium), ("middle", middle), ("cheap", cheap) })
        {
            var p = customers.Arrive(new Order(sunday));
            customers.Tick(1);
            int before = customers.Coins;
            int spentBefore = customers.Today.spent;
            Add(drink);
            if (label == "cheap")
            {
                customers.Select(p.spot);
                Debug.Log($"[Check] recipe with a cheap cup: {customers.recipePanel.title.text} :: {customers.recipePanel.body.text.Replace("\n", " / ")}");
                MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "EconomyCheck.png");
            }
            int cost = customers.Today.spent - spentBefore;
            cup.Serve();
            int profit = customers.Coins - before;
            Debug.Log($"[Check] {label,-7}: cost {cost}, paid {profit + cost}, profit {profit} '{customers.LastReaction?.Replace("\n", " ")}'");
            for (int t = 0; t < 10 && !customers.IsEmpty; t++) customers.Tick(5);
        }

        // WITH asks for a particular choice, never the cheapest: habit pours the wrong one
        var hard = AssetDatabase.LoadAssetAtPath<Balance>("Assets/Data/Balance Hard.asset");
        var addable = browser.root.Ingredients().Where(n => n.cupPixels <= hard.addMaxPixels).ToList();
        int withs = 0, cheapest = 0;
        var examples = new List<string>();
        for (int i = 0; i < 400; i++)
        {
            var recipe = customers.recipes[i % customers.recipes.Count];
            var order = Order.Random(recipe, new Snack[0], hard, 10, addable);
            for (int l = 0; l < recipe.lines.Count; l++)
            {
                var folder = recipe.lines[l].node;
                if (folder.IsIngredient || order.lines[l].node == folder || order.lines[l].removed) continue;
                if (!order.requests.Contains("WITH " + order.lines[l].Name)) continue;
                withs++;
                if (order.lines[l].node.cost == folder.Ingredients().Min(n => n.cost)) cheapest++;
                if (examples.Count < 4) examples.Add($"{recipe.displayName}: WITH {order.lines[l].Name} (${order.lines[l].node.cost}, cheapest ${folder.Ingredients().Min(n => n.cost)})");
            }
        }
        Debug.Log($"[Check] WITH changes: {withs}, naming the cheapest choice: {cheapest}; e.g. {string.Join(" | ", examples)}");
    }

    static void Add(params string[] names)
    {
        foreach (var name in names) cup.Add(browser.root.Ingredients().First(n => n.displayName == name));
    }
}
