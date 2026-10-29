using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for the title screen: first run (no save), NEW GAME, CONTINUE
// from a saved day 3, and the two-click NEW GAME that wipes a save.
// Uses a scratch save file, never the real one.
// Run: Unity -batchmode -quit -projectPath . -executeMethod TitleCheck.Run -captureOut out.png -savePath save.json
public static class TitleCheck
{
    static TitleScreen title;
    static DayCycle day;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        title = Object.FindAnyObjectByType<TitleScreen>();
        day = title.day;
        SaveSystem.PathOverride = MatchaSceneBuilder.Arg("-savePath") ?? "Temp/TitleCheckSave.json";
        SaveSystem.Delete();
        day.customers.Init();

        title.Show();
        Log("first run");
        title.NewGame();
        Log("new game -> pick");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut")?.Replace(".png", "_difficulty.png") ?? "TitleDifficulty.png");
        title.ShowMain(); // what BACK does
        Log("back");
        title.NewGame();
        title.Choose(0);
        Log("chose easy");

        SaveSystem.Save(new SaveData { day = 3, coins = 120, bestHappy = 7 });
        title.Show();
        Log("has save");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "TitleCheck.png");
        title.Continue();
        Log("continued");

        title.Show();
        title.NewGame();
        Log("new game once");
        title.NewGame();
        Log("new game twice");
        title.Choose(2);
        Log("chose hard");

        SaveSystem.Delete();
        SaveSystem.PathOverride = null;
    }

    static void Log(string step)
    {
        Debug.Log($"[Check] {step,-15} title={title.Showing} continue={(title.continueButton.interactable ? "on" : "off")} " +
                  $"info='{title.continueInfo.text}' newGame='{title.newGameLabel.text}' running={day.Running} " +
                  $"{day.dayText.text} coins={day.customers.Coins} mode={(day.balance != null ? day.balance.difficultyName : "-")} picking={title.ChoosingDifficulty} save={(SaveSystem.Exists ? SaveSystem.Load().day + "/" + SaveSystem.Load().coins : "none")}");
    }
}
