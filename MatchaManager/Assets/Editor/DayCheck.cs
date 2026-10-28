using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for the day cycle: clock steps, a happy and a walked-out
// customer, closing while someone is still waiting, the summary, day 2, and
// saving/loading (to a scratch save file, never the real one).
// Run: Unity -batchmode -quit -projectPath . -executeMethod DayCheck.Run -captureOut out.png
public static class DayCheck
{
    static DayCycle day;
    static CustomerManager customers;
    static DrinkBrowser browser;
    static Cup cup;
    static DrinkRecipe sunday;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        day = Object.FindAnyObjectByType<DayCycle>();
        customers = day.customers;
        browser = Object.FindAnyObjectByType<DrinkBrowser>();
        cup = day.cup;
        sunday = customers.recipes.First(r => r.displayName == "Sunday Matcha");
        Random.InitState(3);
        SaveSystem.PathOverride = MatchaSceneBuilder.Arg("-savePath") ?? "Temp/DayCheckSave.json";
        SaveSystem.Delete();
        customers.Init();
        customers.autoArrive = false;
        day.LoadSave();
        day.BeginDay();
        Debug.Log("[Check] save after opening: " + SaveFile());
        browser.ShowRoot();
        Log("opened");

        day.Tick(1); Log("1s");
        day.Tick(74); Log("75s");

        ServeCorrect(); Log("happy customer");

        customers.Arrive(new Order(sunday));
        customers.Tick(1);
        customers.Tick(100); customers.Tick(5); customers.Tick(5);
        Log("walked out");

        // closing time arrives while a customer is waiting: they still get served
        customers.Arrive(new Order(sunday));
        customers.Tick(1);
        day.Tick(300); Log("closing, waiting");
        Make();
        cup.Serve();
        customers.Tick(5); customers.Tick(5); customers.Tick(10);
        Log("last one left");
        day.Tick(0.1f); Log("summary");
        Debug.Log($"[Check] summary: {day.summaryTitle.text} :: {day.summaryBody.text.Replace("\n", " / ")}");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "DayCheck.png");
        Debug.Log("[Check] save at summary: " + SaveFile());

        day.NextDay(); Log("next day");
        Debug.Log($"[Check] day 2 patienceScale={customers.patienceScale:0.00}");
        Debug.Log("[Check] save on day 2: " + SaveFile());

        // quit mid-day 2 after earning something: reloading goes back to the start of day 2
        ServeCorrect();
        Log("earned on day 2");
        day.LoadSave();
        day.BeginDay();
        Log("reloaded");
        SaveSystem.Delete();
        SaveSystem.PathOverride = null;
    }

    static string SaveFile() => System.IO.File.ReadAllText(SaveSystem.FilePath).Replace("\n", " ").Replace("    ", "");

    static void ServeCorrect()
    {
        customers.Arrive(new Order(sunday));
        customers.Tick(1);
        Make();
        cup.Serve();
        customers.Tick(5); customers.Tick(5);
    }

    static void Make()
    {
        foreach (var name in new[] { "Ceremonial Matcha", "Water", "Oat Milk", "Cane Sugar" })
            cup.Add(browser.root.Ingredients().First(n => n.displayName == name));
    }

    static void Log(string step)
    {
        var t = customers.Today;
        Debug.Log($"[Check] {step,-18} {day.dayText.text} clock='{day.clockText.text}' open={day.IsOpen} ended={day.Ended} " +
                  $"empty={customers.IsEmpty} summary={day.summary.activeSelf} {customers.servedText.text} " +
                  $"dots={customers.servedDots.Count(d => d.activeSelf)} coins={customers.Coins} " +
                  $"today(served={t.served} happy={t.happy} wrong={t.wrong} out={t.walkedOut} earned={t.earned})");
    }
}
