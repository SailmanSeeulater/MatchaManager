using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for unclear orders: which drinks can be mumbled and as what,
// asking (wrong guess costs everyone patience, right guess reveals the order and
// its changes), the peek sliding along the mumble, the bonus for making one right without
// asking (smaller the more the peek has shown), and that Easy never mumbles.
// Run: Unity -batchmode -quit -projectPath . -executeMethod AmbiguityCheck.Run -captureOut out.png
public static class AmbiguityCheck
{
    static CustomerManager customers;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Object.FindAnyObjectByType<TitleScreen>().gameObject.SetActive(false);
        customers = Object.FindAnyObjectByType<CustomerManager>();
        var browser = Object.FindAnyObjectByType<DrinkBrowser>();
        var cup = customers.cup;
        Random.InitState(4);
        customers.Init();
        customers.autoArrive = false;
        browser.ShowRoot();

        foreach (var d in MatchaSceneBuilder.EnsureDifficulties())
            Debug.Log($"[Check] {d.difficultyName,-6} unclear chance: day 1 {d.UnclearChance(1):P0}, day 10 {d.UnclearChance(10):P0}");

        // what each drink can be mumbled as
        foreach (var recipe in customers.recipes)
        {
            var p = new CustomerManager.Patron { order = new Order(recipe) };
            bool unclear = customers.MakeUnclear(p);
            Debug.Log($"[Check] {recipe.displayName,-18} -> " + (unclear
                ? $"'{p.order.clue}' guesses [{string.Join(" | ", p.guesses.Select(g => g.displayName))}]"
                : "always said clearly (no shared word)"));
        }

        // ask, guess wrong, then right
        var sunday = customers.recipes.First(r => r.displayName == "Sunday Matcha");
        var hard = MatchaSceneBuilder.EnsureDifficulties()[2];
        Order picky = null;
        while (picky == null || picky.requests.Count < 2) picky = Order.Random(sunday, customers.snacks.Menu, hard, 10);
        var mumbler = customers.Arrive(picky, true);
        var other = customers.Arrive(new Order(customers.recipes.First(r => r.displayName == "Mocha")));
        customers.Tick(1);
        Log("arrived");
        customers.Select(mumbler.spot);
        for (int i = 0; i < 4; i++)
        {
            customers.Tick(0.7f);
            Log($"peek {mumbler.peekTime:0.0}s seen {mumbler.seen:P0} bonus {customers.GuessBonusNow(mumbler)}");
        }
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut")?.Replace(".png", "_peek.png") ?? "AmbiguityPeek.png");
        customers.ClickTicket(mumbler.spot);
        Log("asked");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "AmbiguityCheck.png");
        int wrong = mumbler.guesses.FindIndex(g => g != picky.recipe);
        float before = other.patience;
        customers.Guess(wrong);
        Log($"guessed {mumbler.guesses[wrong].displayName}");
        Debug.Log($"[Check] other customer's patience {before:0.00} -> {other.patience:0.00}; guesses now '{string.Join(" | ", customers.guessLabels.Select(l => l.text))}'");
        customers.Guess(mumbler.guesses.IndexOf(picky.recipe));
        Log("guessed right");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut")?.Replace(".png", "_revealed.png") ?? "AmbiguityRevealed.png");

        // making an unclear order right without asking earns a bonus: all of it straight away,
        // none once the peek has shown the whole order
        foreach (float watch in new[] { 0f, 2f, 10f })
        {
            for (int t = 0; t < 100 && !customers.IsEmpty; t++) customers.Tick(5);
            var gamble = customers.Arrive(new Order(sunday), true);
            for (float w = 0; w < watch; w += 0.25f) customers.Tick(0.25f);
            foreach (var line in gamble.order.lines.Where(l => !l.removed))
                for (int i = 0; i < line.portions; i++) cup.Add(line.node.Ingredients().First());
            gamble.patience = 0.5f; // same tip each time, so only the bonus differs
            int coins = customers.Coins + customers.Today.spent;
            float seen = gamble.seen;
            cup.Serve();
            Debug.Log($"[Check] served unclear Sunday Matcha after watching {watch:0}s (seen {seen:P0}) -> '{customers.LastReaction?.Replace("\n", " ")}' paid={customers.Coins + customers.Today.spent - coins} (price {gamble.order.Price})");
        }
    }

    static void Log(string step)
    {
        var t = customers.pickup.Select(p => p.group.alpha > 0 ? p.ticketText.text.Replace("\n", " / ") : "-");
        Debug.Log($"[Check] {step,-28} guessing={customers.Guessing} tickets=[{string.Join(" || ", t)}]\n" +
                  $"        recipe: {customers.recipePanel.title.text} :: {customers.recipePanel.body.text.Replace("\n", " / ")}");
    }
}
