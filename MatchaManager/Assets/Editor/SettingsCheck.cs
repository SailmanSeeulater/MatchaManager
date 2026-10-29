using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for the pause menu, the settings page and the music player: which music
// plays on the title, in the shop, paused (quieter) and at the summary; pausing stops time;
// each setting changes and sticks; QUIT TO TITLE closes the shop; the title opens settings.
// Uses made-up silent clips. Puts the real PlayerPrefs settings back afterwards.
// Run: Unity -batchmode -quit -projectPath . -executeMethod SettingsCheck.Run -captureOut out.png -savePath save.json
public static class SettingsCheck
{
    static PauseMenu pause;
    static MusicPlayer music;
    static DayCycle day;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SaveSystem.PathOverride = MatchaSceneBuilder.Arg("-savePath") ?? "Temp/SettingsCheckSave.json";
        SaveSystem.Delete();
        pause = Resources.FindObjectsOfTypeAll<PauseMenu>().First();
        music = Object.FindAnyObjectByType<MusicPlayer>();
        day = Object.FindAnyObjectByType<DayCycle>();
        var title = Object.FindAnyObjectByType<TitleScreen>();
        var customers = day.customers;
        GameSettings.Load();
        float music0 = GameSettings.MusicVolume, sound0 = GameSettings.SoundVolume;
        bool full0 = GameSettings.Fullscreen, hints0 = GameSettings.KeyHints;

        // what Play mode would do on startup
        Call(customers.cup, "Awake");
        foreach (var mb in new MonoBehaviour[] { title, day, pause.keys }) Call(mb, "Start");
        customers.Init();
        customers.autoArrive = false;
        music.titleTracks = new[] { Clip("TitleTune") };
        music.shopTracks = new[] { Clip("ShopTune1"), Clip("ShopTune2") };
        music.summaryTracks = new[] { Clip("SummaryTune") };
        Debug.Log($"[Check] scene music: {music.titleTracks.Length}/{music.shopTracks.Length}/{music.summaryTracks.Length} tracks, pause hidden={!pause.gameObject.activeSelf}");

        Music("title");
        KeyboardShortcuts.Press(title.newGameButton);
        KeyboardShortcuts.Press(title.difficultyButtons[1]);
        Music("shop");

        pause.Pause();
        Call(pause, "Awake"); // its listeners, as on the first real open
        Log("paused");
        Music("paused");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut")?.Replace(".png", "_pause.png") ?? "SettingsPause.png");
        KeyboardShortcuts.Press(pause.settingsButton);
        Log("settings");
        for (int i = 0; i < 3; i++) KeyboardShortcuts.Press(pause.musicDown);
        KeyboardShortcuts.Press(pause.soundUp);
        KeyboardShortcuts.Press(pause.fullscreenButton);
        KeyboardShortcuts.Press(pause.hintsButton);
        Log("changed settings");
        Music("paused, quieter music");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "SettingsCheck.png");
        KeyboardShortcuts.Press(pause.hintsButton);
        pause.Back();
        Log("back");
        pause.Back();
        Log("resumed");

        // QUIT TO TITLE needs two clicks
        customers.Arrive(new Order(customers.recipes[0]));
        pause.Pause();
        KeyboardShortcuts.Press(pause.quitButton);
        Log("quit once");
        KeyboardShortcuts.Press(pause.quitButton);
        Log("quit twice");
        Music("back on the title");

        // the title's SETTINGS button
        KeyboardShortcuts.Press(title.settingsButton);
        Log("settings from title");
        pause.Back();
        Log("back from title settings");

        // the summary has its own music; pausing isn't possible there
        KeyboardShortcuts.Press(title.continueButton);
        day.Tick(10000);
        Music("summary");
        pause.Pause();
        Log("ESC at the summary");

        GameSettings.SetMusicVolume(music0);
        GameSettings.SetSoundVolume(sound0);
        GameSettings.SetFullscreen(full0);
        GameSettings.SetKeyHints(hints0);
        Time.timeScale = 1;
        SaveSystem.Delete();
        SaveSystem.PathOverride = null;
    }

    static AudioClip Clip(string name) => AudioClip.Create(name, 4410, 1, 44100, false);

    static void Music(string when)
    {
        music.Tick(5); // long enough for any crossfade
        var sources = music.GetComponents<AudioSource>();
        Debug.Log($"[Check] music {when,-22} mood={music.Current} clip={(music.Playing != null ? music.Playing.name : "-")} " +
                  $"volume={sources.Max(s => s.volume):0.00} (setting {GameSettings.MusicVolume:0.0})");
    }

    static void Log(string step)
    {
        Debug.Log($"[Check] {step,-26} showing={pause.Showing} paused={pause.Paused} settings={pause.InSettings} timeScale={Time.timeScale} " +
                  $"running={day.Running} title={day.title.Showing} waiting={day.customers.WaitingCount} quit='{pause.quitLabel.text}' " +
                  $"music={GameSettings.MusicVolume:0.0} bars={pause.musicBars.Count(b => b.color.g > 0.6f && b.color.r < 0.5f)} sound={GameSettings.SoundVolume:0.0} " +
                  $"fullscreen={pause.fullscreenLabel.text} hints={pause.hintsLabel.text} hintsShown={pause.keys.hints.Count(h => h.activeSelf)}/{pause.keys.hints.Length}");
    }

    static void Call(MonoBehaviour target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Invoke(target, null);
}
