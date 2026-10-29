using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for sound wiring: every Sfx has an imported clip, the click
// buttons are all set, and the scene has an AudioListener to hear them.
// Run: Unity -batchmode -quit -projectPath . -executeMethod SoundCheck.Run
public static class SoundCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var sound = Object.FindAnyObjectByType<SoundFX>();
        foreach (var e in sound.sounds)
            Debug.Log($"[Check] {e.id,-10} clip={(e.clip != null ? e.clip.name : "MISSING")} " +
                      $"length={(e.clip != null ? e.clip.length : 0):0.00}s volume={e.volume} jitter={e.pitchJitter}");
        Debug.Log($"[Check] {sound.sounds.Count(e => e.clip != null)}/{System.Enum.GetValues(typeof(Sfx)).Length} sounds have clips; " +
                  $"click buttons: {sound.clickButtons.Count(b => b != null)}/{sound.clickButtons.Length} set; " +
                  $"audio listener: {Object.FindAnyObjectByType<AudioListener>() != null}");
    }
}
