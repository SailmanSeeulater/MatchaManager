using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ESC while the shop is open: the clock, customers and snacks stop (time scale 0) and this
// overlay offers RESUME, SETTINGS and QUIT TO TITLE (two clicks, since it replays the day).
// The settings page (also opened from the title) sets music and sound volume, fullscreen
// and the key hints; GameSettings keeps them between sessions.
public class PauseMenu : MonoBehaviour
{
    public DayCycle day;
    public TitleScreen title;
    public KeyboardShortcuts keys;

    [Header("Pause page")]
    public GameObject pausePage;
    public Button resumeButton;
    public Button settingsButton;
    public Button quitButton;
    public TMP_Text quitLabel;

    [Header("Settings page")]
    public GameObject settingsPage;
    public Button musicDown, musicUp, soundDown, soundUp;
    public Image[] musicBars, soundBars;
    public Button fullscreenButton, hintsButton;
    public TMP_Text fullscreenLabel, hintsLabel;
    public Button backButton;

    [Tooltip("Seconds the \"sure?\" prompt on QUIT waits for the second click")]
    public float confirmSeconds = 3;

    static readonly Color BarOn = new Color32(0x6f, 0xa0, 0x4a, 0xff);
    static readonly Color BarOff = new Color32(0xd8, 0xcc, 0xb0, 0xff);

    bool fromTitle;     // settings opened from the title: BACK closes the overlay
    float confirmTimer; // unscaled, since the game is paused

    public bool Showing => gameObject.activeSelf;
    public bool Paused => Showing && !fromTitle;
    public bool InSettings => Showing && settingsPage.activeSelf;

    // The overlay starts hidden, so this runs the first time it opens.
    void Awake()
    {
        resumeButton.onClick.AddListener(Resume);
        settingsButton.onClick.AddListener(() => ShowSettings(false));
        quitButton.onClick.AddListener(Quit);
        musicDown.onClick.AddListener(() => GameSettings.SetMusicVolume(GameSettings.MusicVolume - GameSettings.Step));
        musicUp.onClick.AddListener(() => GameSettings.SetMusicVolume(GameSettings.MusicVolume + GameSettings.Step));
        soundDown.onClick.AddListener(() => GameSettings.SetSoundVolume(GameSettings.SoundVolume - GameSettings.Step));
        soundUp.onClick.AddListener(() => GameSettings.SetSoundVolume(GameSettings.SoundVolume + GameSettings.Step));
        fullscreenButton.onClick.AddListener(() => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
        hintsButton.onClick.AddListener(() => GameSettings.SetKeyHints(!GameSettings.KeyHints));
        backButton.onClick.AddListener(Back);
        GameSettings.Load();
        GameSettings.Changed += Refresh;
    }

    void OnDestroy() => GameSettings.Changed -= Refresh;

    void Update()
    {
        if (confirmTimer <= 0) return;
        confirmTimer -= Time.unscaledDeltaTime;
        if (confirmTimer <= 0) quitLabel.text = "QUIT TO TITLE";
    }

    // Alt-tabbing out of a built game pauses it.
    void OnApplicationFocus(bool focused)
    {
        if (!focused && !Application.isEditor) Pause();
    }

    // Only while a day is under way (not on the title or the summary).
    public void Pause()
    {
        if (Showing || day == null || !day.Running || day.Ended || (title != null && title.Showing)) return;
        fromTitle = false;
        gameObject.SetActive(true);
        Time.timeScale = 0;
        ShowPausePage();
    }

    public void Resume()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1;
    }

    // From the title screen's SETTINGS button.
    public void OpenFromTitle()
    {
        fromTitle = true;
        gameObject.SetActive(true);
        ShowSettings(true);
    }

    void ShowPausePage()
    {
        pausePage.SetActive(true);
        settingsPage.SetActive(false);
        confirmTimer = 0;
        quitLabel.text = "QUIT TO TITLE";
    }

    void ShowSettings(bool openedFromTitle)
    {
        fromTitle = openedFromTitle;
        pausePage.SetActive(false);
        settingsPage.SetActive(true);
        Refresh();
    }

    // ESC / BACK: settings -> pause page (or out, from the title); pause page -> resume.
    public void Back()
    {
        if (!Showing) return;
        if (InSettings && !fromTitle) ShowPausePage();
        else if (fromTitle) gameObject.SetActive(false);
        else Resume();
    }

    public void Quit()
    {
        if (confirmTimer <= 0)
        {
            confirmTimer = confirmSeconds;
            quitLabel.text = "<color=#8a2e2c>SURE? CLICK AGAIN</color>";
            return;
        }
        Resume();
        day.CloseShop();
        title.Show();
    }

    void Refresh()
    {
        if (settingsPage == null) return;
        Bars(musicBars, GameSettings.MusicVolume);
        Bars(soundBars, GameSettings.SoundVolume);
        musicDown.interactable = GameSettings.MusicVolume > 0;
        musicUp.interactable = GameSettings.MusicVolume < 1;
        soundDown.interactable = GameSettings.SoundVolume > 0;
        soundUp.interactable = GameSettings.SoundVolume < 1;
        fullscreenLabel.text = GameSettings.Fullscreen ? "ON" : "OFF";
        hintsLabel.text = GameSettings.KeyHints ? "ON" : "OFF";
        if (keys != null) keys.ShowHints(GameSettings.KeyHints);
    }

    static void Bars(Image[] bars, float volume)
    {
        int on = Mathf.RoundToInt(volume * bars.Length);
        for (int i = 0; i < bars.Length; i++) bars[i].color = i < on ? BarOn : BarOff;
    }
}
