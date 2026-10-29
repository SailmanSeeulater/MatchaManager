using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Every sound effect in the game. Clip files live in Assets/Audio/<Sfx name>.wav
// (made by Tools/make_sfx.py). Gameplay code calls SoundFX.Play(Sfx.X); it does
// nothing outside Play mode, so the headless checks stay silent.
// Press M to mute or unmute everything; the settings page sets the sound volume.
public enum Sfx { Click, Pour, CupFull, Dump, ServeGood, ServeBad, Arrive, WalkOut, Pick, Tray, HeatStart, Ready, Burnt, DayEnd, DayStart, Order, Finished }

public class SoundFX : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Sfx id;
        public AudioClip clip;
        [Range(0, 1)] public float volume = 1;
        [Tooltip("Random pitch change each time, so repeats don't sound identical (0.05 = +-5%)")]
        public float pitchJitter;
    }

    public Entry[] sounds;
    [Range(0, 1)] public float masterVolume = 0.8f;
    [Tooltip("Buttons that just play the click sound (tabs, Back, menu, title...)")]
    public Button[] clickButtons;

    const int Voices = 6; // sounds that can overlap at once

    static SoundFX instance;
    readonly Dictionary<Sfx, Entry> lookup = new Dictionary<Sfx, Entry>();
    AudioSource[] voices;
    int nextVoice;

    public static bool Muted { get; private set; }

    void Awake()
    {
        instance = this;
        foreach (var e in sounds) lookup[e.id] = e;
        voices = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }
        foreach (var b in clickButtons)
            if (b != null) b.onClick.AddListener(() => Play(Sfx.Click));
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            Muted = !Muted;
            AudioListener.volume = Muted ? 0 : 1;
        }
    }

    public static void Play(Sfx id)
    {
        if (instance == null || !Application.isPlaying) return;
        instance.PlayNow(id);
    }

    void PlayNow(Sfx id)
    {
        if (!lookup.TryGetValue(id, out var e) || e.clip == null) return;
        // round-robin voices so a new sound doesn't cut off the last one
        var voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % Voices;
        voice.pitch = 1 + UnityEngine.Random.Range(-e.pitchJitter, e.pitchJitter);
        voice.PlayOneShot(e.clip, e.volume * masterVolume * GameSettings.SoundVolume);
    }
}
