using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for cost to operate: default costs and stars, slot labels,
// coins spent as ingredients are poured (and lost on a dump), snack costs, and
// how cheap vs premium drinks pay with casual, regular and picky customers.
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
            Debug.Log($"[Check] {n.displayName,-18} cost ${n.cost} quality {n.quality}");
        var sunday = customers.recipes.First(r => r.displayName == "Sunday Matcha");
        Debug.Log($"[Check] Sunday Matcha lines: {string.Join(", ", new Order(sunday).lines.Select(l => l.Name))}");

        // the matcha folder's buttons show cost and stars
        foreach (var name in new[] { "Flavors", "Tea", "Matcha" })
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

        // cheap vs premium for each palate
        var premium = new[] { "Ceremonial Matcha", "Water", "Pistachio Milk", "Maple Sugar" };
        var middle = new[] { "Ceremonial Matcha", "Water", "Whole Milk", "Cane Sugar" };
        var cheap = new[] { "Low-Grade Matcha", "Water", "Soy Milk", "White Sugar" };
        foreach (int palate in new[] { 1, 2, 3 })
            foreach (var (label, drink) in new[] { ("premium", premium), ("middle", middle), ("cheap", cheap) })
            {
                var p = customers.Arrive(new Order(sunday), false, palate);
                customers.Tick(1);
                int before = customers.Coins;
                int spentBefore = customers.Today.spent;
                Add(drink);
                if (label == "cheap" && palate == 3)
                {
                    customers.Select(p.spot);
                    Debug.Log($"[Check] recipe for a picky customer with a cheap cup: {customers.recipePanel.title.text} :: {customers.recipePanel.body.text.Replace("\n", " / ")}");
                    MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "EconomyCheck.png");
                }
                int cost = customers.Today.spent - spentBefore;
                cup.Serve();
                int profit = customers.Coins - before;
                Debug.Log($"[Check] palate {palate} {label,-7}: cost {cost}, paid {profit + cost}, profit {profit} '{customers.LastReaction?.Replace("\n", " ")}'");
                for (int t = 0; t < 10 && !customers.IsEmpty; t++) customers.Tick(5);
            }
    }

    static void Add(params string[] names)
    {
        foreach (var name in names) cup.Add(browser.root.Ingredients().First(n => n.displayName == name));
    }
}
