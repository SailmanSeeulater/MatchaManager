using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for finished drinks: every recipe has a picture, which recipes overlap
// (one cup that is a correct drink for both), and that finishing a drink shows its picture,
// switches the recipe panel to the customer it's for, and keeps a mumbled order secret.
// Run: Unity -batchmode -quit -projectPath . -executeMethod DrinkSpriteCheck.Run -captureOut out.png
public static class DrinkSpriteCheck
{
    static CustomerManager customers;
    static Cup cup;
    static DrinkBrowser browser;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Object.FindAnyObjectByType<TitleScreen>().gameObject.SetActive(false);
        customers = Object.FindAnyObjectByType<CustomerManager>();
        browser = Object.FindAnyObjectByType<DrinkBrowser>();
        cup = customers.cup;
        customers.Init();
        customers.autoArrive = false;
        browser.ShowRoot();

        foreach (var r in customers.recipes)
            Debug.Log($"[Check] {r.displayName,-18} sprite {(r.sprite != null ? r.sprite.name : "MISSING")}");

        // overlaps: some cup that makes recipe A exactly is also a correct B
        var overlaps = new List<string>();
        foreach (var a in customers.recipes)
            foreach (var cupA in Cups(new Order(a)))
                foreach (var b in customers.recipes)
                {
                    if (b == a || !new Order(b).Check(cupA).Correct) continue;
                    string pair = string.Join(" = ", new[] { a.displayName, b.displayName }.OrderBy(n => n));
                    if (overlaps.Any(o => o.StartsWith(pair))) continue;
                    overlaps.Add($"{pair} (e.g. {string.Join(", ", cupA.Select(kv => kv.Key.displayName + (kv.Value > 1 ? " x" + kv.Value : "")))})");
                }
        Debug.Log($"[Check] {overlaps.Count} overlapping recipe pairs:\n  " + string.Join("\n  ", overlaps));

        // finishing someone else's drink switches the panel to them
        var sunday = Recipe("Sunday Matcha");
        var mocha = Recipe("Mocha");
        customers.Arrive(new Order(sunday));
        customers.Tick(0.5f);
        customers.Arrive(new Order(mocha));
        customers.Tick(0.5f);
        customers.Arrive(new Order(Recipe("White Mocha")));
        customers.Tick(0.5f);
        customers.Select(0);
        Log("3 waiting, #1 selected");
        Make("Arabrica", "White Chocolate");
        Log("2 of 3 mocha lines");
        Make("Whole Milk");
        Log("finished a mocha");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "DrinkSpriteCheck.png");
        cup.Serve();
        Log("served");
        Make("Arabrica", "White Chocolate", "Whole Milk");
        Log("again: only White Mocha left");
        cup.Serve();
        Make("Ceremonial Matcha", "Hot Water", "Whole Milk", "Cane Sugar");
        cup.Serve(); // the Sunday Matcha customer, so only the mumbler will want one
        for (int t = 0; t < 10; t++) customers.Tick(1);

        // a mumbled order's drink doesn't give the answer away
        var mumbler = customers.Arrive(new Order(sunday), true);
        customers.Tick(0.5f);
        customers.Select(mumbler.spot);
        Make("Ceremonial Matcha", "Hot Water", "Whole Milk", "Cane Sugar");
        Log("made the mumbled order");
        cup.Clear();
    }

    static DrinkRecipe Recipe(string name) => customers.recipes.First(r => r.displayName == name);

    // Every cup that makes this order exactly: each "any" line tried with each of its ingredients.
    static IEnumerable<Dictionary<DrinkNode, int>> Cups(Order order)
    {
        IEnumerable<Dictionary<DrinkNode, int>> cups = new[] { new Dictionary<DrinkNode, int>() };
        foreach (var line in order.lines.Where(l => !l.removed))
        {
            var options = line.node.Ingredients().ToList();
            cups = cups.SelectMany(c => options.Select(leaf =>
            {
                var next = new Dictionary<DrinkNode, int>(c);
                next[leaf] = (next.TryGetValue(leaf, out var n) ? n : 0) + line.portions;
                return next;
            }));
        }
        return cups;
    }

    static void Make(params string[] names)
    {
        foreach (var name in names) cup.Add(browser.root.Ingredients().First(n => n.displayName == name));
    }

    static void Log(string step)
    {
        Debug.Log($"[Check] {step,-30} finished={(cup.finishedView.enabled ? cup.finishedView.sprite.name : "-")} glass={cup.glass.enabled} " +
                  $"readout='{cup.readout.text.Replace("\n", " / ")}' selected=#{customers.Selected?.spot + 1} panel='{customers.recipePanel.title.text}'");
    }
}
