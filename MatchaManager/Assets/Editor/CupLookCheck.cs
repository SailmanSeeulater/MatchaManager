using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for how ingredients look in the glass: fills a cup with a liquid,
// a powder, ice and more liquid, logs each ingredient's look, and renders it.
// Run: Unity -batchmode -quit -projectPath . -executeMethod CupLookCheck.Run -captureOut out.png
public static class CupLookCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Object.FindAnyObjectByType<TitleScreen>().gameObject.SetActive(false);
        var browser = Object.FindAnyObjectByType<DrinkBrowser>();
        var cup = browser.cup;
        Random.InitState(5);
        foreach (var n in browser.root.Ingredients())
            Debug.Log($"[Check] {n.displayName,-18} look={n.look} px={n.cupPixels} icon={(n.icon != null ? n.icon.name : "none")}");
        foreach (var name in new[] { "Ceremonial Matcha", "Water", "Ice", "Ice", "Oat Milk", "Cane Sugar", "Cane Sugar", "Brown Sugar" })
            cup.Add(browser.root.Ingredients().First(n => n.displayName == name));
        Debug.Log($"[Check] cup children: {cup.contents.childCount}, ice cubes: {cup.iceFront.childCount}, front is last: {cup.iceFront.GetSiblingIndex() == cup.contents.childCount - 1}");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "CupLookCheck.png");
        cup.Clear();
        Debug.Log($"[Check] after clear: cubes left={cup.iceFront.childCount}, layers left={cup.contents.childCount - 1}");
    }
}
