using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for the café flow: customers arrive into the line, order at the
// counter one at a time, and wait at the 3 pickup spots; when pickup is full the
// line backs up and then overflows. Serving matches the drink to a ticket
// (oldest first), a drink nobody ordered stays in the cup, arrow keys switch
// tickets, and everyone eventually gives up if left waiting.
// Run: Unity -batchmode -quit -projectPath . -executeMethod QueueCheck.Run -captureOut out.png
public static class QueueCheck
{
    static CustomerManager customers;
    static DrinkBrowser browser;
    static SnackStation snacks;
    static Cup cup;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Object.FindAnyObjectByType<TitleScreen>().gameObject.SetActive(false); // straight to the shop
        customers = Object.FindAnyObjectByType<CustomerManager>();
        browser = Object.FindAnyObjectByType<DrinkBrowser>();
        snacks = customers.snacks;
        cup = customers.cup;
        Random.InitState(11);
        customers.balance = Object.Instantiate(customers.balance); // a copy, so the real asset is untouched
        customers.balance.arrivalInterval = new Vector2(3, 3);
        customers.Init();
        browser.ShowRoot();

        for (int t = 1; t <= 17; t++)
        {
            customers.Tick(1);
            Log($"t={t}s");
        }
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "QueueCheck.png");

        // arrow keys move the selection between waiting tickets
        customers.SelectNext(1); Log("right");
        customers.SelectNext(1); Log("right");
        customers.SelectNext(-1); Log("left");

        // a drink nobody ordered stays in the cup
        cup.Add(browser.root.Ingredients().First(n => n.displayName == "Water"));
        cup.Serve();
        Debug.Log($"[Check] water only -> cup empty={cup.IsEmpty}, readout='{cup.readout.text.Replace("\n", " / ")}', waiting={customers.WaitingCount}");
        cup.Clear();

        // make the selected ticket exactly, snack too, and serve it
        MakeOrder(customers.CurrentOrder);
        int spot = customers.Selected.spot;
        cup.Serve();
        Log($"served #{spot + 1}");
        Debug.Log($"[Check] reaction='{customers.LastReaction?.Replace("\n", " ")}'");
        for (int t = 0; t < 4; t++) { customers.Tick(1); Log("after serve"); }
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut")?.Replace(".png", "_ordering.png") ?? "QueueOrdering.png");

        // two identical orders: the one who ordered first gets the drink
        customers.autoArrive = false;
        for (int t = 0; t < 100 && !customers.IsEmpty; t++) customers.Tick(5); // let everyone give up and leave
        Log("all gave up");
        var sunday = customers.recipes.First(r => r.displayName == "Sunday Matcha");
        var first = customers.Arrive(new Order(sunday));
        customers.Tick(1);
        var second = customers.Arrive(new Order(sunday));
        MakeOrder(new Order(sunday));
        cup.Serve();
        Debug.Log($"[Check] identical orders: first is {first.stage}, second is {second.stage}");
    }

    static void MakeOrder(Order order)
    {
        foreach (var line in order.lines.Where(l => !l.removed))
            for (int i = 0; i < line.portions; i++) cup.Add(line.node.Ingredients().First());
        if (order.snack == null) return;
        int slot = System.Array.FindIndex(snacks.slots, s => s.snack == order.snack);
        snacks.ClickSlot(slot);
        if (!order.snack.needsHeating) return;
        snacks.ClickAppliance(0);
        snacks.Tick(5);
        snacks.ClickAppliance(0);
    }

    static void Log(string step)
    {
        var views = string.Join(",", customers.lineViews.Select(v => v.enabled ? "L" : "-"));
        var pickup = string.Join(",", customers.pickup.Select(p => p.group.alpha > 0 ? p.ticketText.text.Split('\n')[0] : "-"));
        Debug.Log($"[Check] {step,-12} line={customers.LineLength}[{views}] ordering={customers.SomeoneOrdering} " +
                  $"pickup=[{pickup}] selected=#{(customers.Selected != null ? customers.Selected.spot + 1 : 0)} " +
                  $"out={customers.Today.walkedOut} served={customers.Today.served} happy={customers.Today.happy}");
    }
}
