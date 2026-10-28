using System;
using System.IO;
using UnityEngine;

// Progress that survives quitting: which day you're on, your coins, and a few
// all-time stats. Stored as JSON in Application.persistentDataPath/save.json.
[Serializable]
public class SaveData
{
    public int day = 1;
    public int coins;
    public int bestHappy;     // most happy customers in one day
    public int totalServed;
    public int totalHappy;
}

public static class SaveSystem
{
    // The headless checks point this at a scratch file so they never touch your real save.
    public static string PathOverride;

    public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "save.json");

    public static SaveData Load()
    {
        if (!File.Exists(FilePath)) return new SaveData();
        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            Debug.Log($"[Save] Loaded day {data.day}, {data.coins} coins from {FilePath}");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Couldn't read {FilePath}, starting fresh: {e.Message}");
            return new SaveData();
        }
    }

    public static void Save(SaveData data)
    {
        // write to a temp file first so a crash mid-write can't leave a half-written save
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonUtility.ToJson(data, true));
        if (File.Exists(FilePath)) File.Delete(FilePath);
        File.Move(temp, FilePath);
    }

    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
