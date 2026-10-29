using System;
using UnityEngine;

// Player options, kept between sessions in PlayerPrefs (not in the save file, so a new
// game keeps them). Changed from the settings page; F1 flips KeyHints too.
public static class GameSettings
{
    const string MusicKey = "Matcha.MusicVolume", SoundKey = "Matcha.SoundVolume",
                 FullscreenKey = "Matcha.Fullscreen", HintsKey = "Matcha.KeyHints";
    public const float Step = 0.1f; // volume changes in tenths

    public static float MusicVolume { get; private set; } = 0.7f;
    public static float SoundVolume { get; private set; } = 0.8f;
    public static bool Fullscreen { get; private set; } = true;
    public static bool KeyHints { get; private set; } = true;

    public static event Action Changed;

    static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, MusicVolume);
        SoundVolume = PlayerPrefs.GetFloat(SoundKey, SoundVolume);
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Fullscreen ? 1 : 0) == 1;
        KeyHints = PlayerPrefs.GetInt(HintsKey, KeyHints ? 1 : 0) == 1;
        ApplyFullscreen();
    }

    public static void SetMusicVolume(float v) { MusicVolume = Tenths(v); Save(); }
    public static void SetSoundVolume(float v) { SoundVolume = Tenths(v); Save(); }
    public static void SetFullscreen(bool on) { Fullscreen = on; ApplyFullscreen(); Save(); }
    public static void SetKeyHints(bool on) { KeyHints = on; Save(); }

    static float Tenths(float v) => Mathf.Clamp01(Mathf.Round(v / Step) * Step);

    // Only in a built game: the editor's Game view decides its own size.
    static void ApplyFullscreen()
    {
        if (Application.isEditor) return;
        Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    static void Save()
    {
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        PlayerPrefs.SetFloat(SoundKey, SoundVolume);
        PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(HintsKey, KeyHints ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
