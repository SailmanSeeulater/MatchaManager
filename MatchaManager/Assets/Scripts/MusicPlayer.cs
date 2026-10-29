using UnityEngine;

// Background music: one set of tracks for the title screen, one for while the shop is open,
// one for the end-of-day summary. Switching crossfades; several shop tracks play one after
// another, a single track loops. Pausing turns the music down. Volume comes from the settings.
// Build Scene fills the tracks from Assets/Audio/Music: files whose names start with
// "Title", "Shop" or "Summary" (e.g. "Shop 2 - Rainy Day.ogg"). Missing tracks = silence.
public class MusicPlayer : MonoBehaviour
{
    public enum Mood { None, Title, Shop, Summary }

    public AudioClip[] titleTracks;
    public AudioClip[] shopTracks;
    public AudioClip[] summaryTracks;

    [Range(0, 1)] public float volume = 0.6f;
    [Tooltip("Seconds to fade from one piece of music to the next")]
    public float fadeSeconds = 1.5f;
    [Tooltip("Music volume while the game is paused, as a share of normal")]
    [Range(0, 1)] public float pausedVolume = 0.4f;

    [Header("What's on screen")]
    public TitleScreen title;
    public DayCycle day;
    public PauseMenu pause;

    AudioSource[] sources;
    int active;        // the source fading in; the other fades out
    float fade = 1;    // 0..1 progress of the current crossfade
    int shopIndex;

    public Mood Current { get; private set; }
    public AudioClip Playing => sources != null ? sources[active].clip : null;

    void Awake()
    {
        sources = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].volume = 0;
        }
        GameSettings.Load();
    }

    // Unscaled: the music keeps going while the game is paused.
    void Update() => Tick(Time.unscaledDeltaTime);

    public void Tick(float dt)
    {
        if (sources == null) Awake();
        var mood = title != null && title.Showing ? Mood.Title
            : day != null && day.summary != null && day.summary.activeSelf ? Mood.Summary
            : day != null && day.Running ? Mood.Shop
            : Mood.None;
        if (mood != Current) SwitchTo(mood);
        else if (mood == Mood.Shop && shopTracks.Length > 1 && Application.isPlaying && !sources[active].isPlaying)
            SwitchTo(Mood.Shop, true); // that track ended: next one

        fade = fadeSeconds > 0 ? Mathf.Min(1, fade + dt / fadeSeconds) : 1;
        float full = volume * GameSettings.MusicVolume * (pause != null && pause.Paused ? pausedVolume : 1);
        sources[active].volume = full * fade;
        var other = sources[1 - active];
        other.volume = Mathf.Min(other.volume, full * (1 - fade));
        if (other.volume <= 0 && other.isPlaying) other.Stop();
    }

    void SwitchTo(Mood mood, bool nextShopTrack = false)
    {
        Current = mood;
        if (nextShopTrack) shopIndex++;
        var tracks = mood == Mood.Title ? titleTracks : mood == Mood.Shop ? shopTracks : mood == Mood.Summary ? summaryTracks : null;
        var clip = tracks != null && tracks.Length > 0 ? tracks[(mood == Mood.Shop ? shopIndex : 0) % tracks.Length] : null;
        if (clip == sources[active].clip && sources[active].isPlaying) return; // same music: keep playing
        active = 1 - active;
        fade = 0;
        var source = sources[active];
        source.clip = clip;
        source.loop = tracks == null || tracks.Length <= 1;
        source.volume = 0;
        if (clip != null && Application.isPlaying) source.Play();
    }
}
