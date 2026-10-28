using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for the customer line: people arrive, fill the line, the
// line overflows, someone gives up waiting, and the next steps up after a serve.
// Run: Unity -batchmode -quit -projectPath . -executeMethod QueueCheck.Run -captureOut out.png
public static class QueueCheck
{
    static CustomerManager customers;
    static DrinkBrowser browser;
    static Cup cup;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        customers = Object.FindAnyObjectByType<CustomerManager>();
        browser = Object.FindAnyObjectByType<DrinkBrowser>();
        cup = customers.cup;
        Random.InitState(11);
        customers.arrivalInterval = new Vector2(3, 3);
        customers.Init();
        browser.ShowRoot();

        for (int t = 1; t <= 12; t++)
        {
            customers.Tick(1);
            Log($"t={t}s");
        }
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "QueueCheck.png");

        // serve the one at the counter whatever they asked for (makes their exact drink)
        var order = customers.CurrentOrder;
        foreach (var line in order.lines.Where(l => !l.removed))
            for (int i = 0; i < line.portions; i++) cup.Add(line.node.Ingredients().First());
        if (order.snack != null) Debug.Log("[Check] (front wanted a snack too; expect a snack complaint)");
        cup.Serve();
        Log("served front");
        for (int t = 0; t < 4; t++) { customers.Tick(1); Log("after serve"); }

        // stop arrivals and let the line run out of patience
        customers.autoArrive = false;
        for (int t = 0; t < 12; t++) customers.Tick(10);
        Log("waited 120s");
    }

    static void Log(string step)
    {
        var views = string.Join(",", customers.lineViews.Select(v => v.enabled ? v.sprite.name : "-"));
        Debug.Log($"[Check] {step,-12} front='{customers.CurrentOrder?.recipe.displayName}' line={customers.LineLength} views=[{views}] " +
                  $"patience={customers.patienceFill.fillAmount:0.00} out={customers.Today.walkedOut} served={customers.Today.served} happy={customers.Today.happy}");
    }
}
