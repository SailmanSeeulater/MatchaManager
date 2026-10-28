using UnityEditor;
using UnityEngine;

// Matcha > Delete Save: start over at day 1 with 0 coins next time you press Play.
// Matcha > Show Save File: open the folder with save.json in Finder.
public static class SaveMenu
{
    [MenuItem("Matcha/Delete Save")]
    public static void DeleteSave()
    {
        SaveSystem.Delete();
        Debug.Log("[Save] Deleted " + SaveSystem.FilePath);
    }

    [MenuItem("Matcha/Show Save File")]
    public static void ShowSave()
    {
        if (System.IO.File.Exists(SaveSystem.FilePath)) EditorUtility.RevealInFinder(SaveSystem.FilePath);
        else Debug.Log("[Save] No save yet. It's created when a day starts: " + SaveSystem.FilePath);
    }
}
