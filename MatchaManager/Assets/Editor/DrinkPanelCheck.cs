using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless check for the drink panel: clicks through the tree by name the way a
// player would, logs each step, and renders the result. Not saved to the scene.
// Run: Unity -batchmode -quit -projectPath . -executeMethod DrinkPanelCheck.Run -captureOut out.png
public static class DrinkPanelCheck
{
    static DrinkBrowser browser;
    static Cup cup;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Object.FindAnyObjectByType<TitleScreen>().gameObject.SetActive(false); // straight to the shop
        browser = Object.FindAnyObjectByType<DrinkBrowser>();
        cup = browser.cup;
        browser.ShowRoot();
        Log("start");

        Click("Flavors"); Click("Tea"); Click("Matcha"); Click("Ceremonial Matcha");
        Back(); Back(); Back();
        Click("Misc"); Click("Water"); Back();
        Click("Dairy"); Click("Non-Dairy"); Click("Oat Milk"); Back(); Back();
        Click("Sweeteners"); Click("Cane Sugar"); Click("Cane Sugar");

        var counts = cup.Counts();
        Debug.Log("[Check] cup = " + string.Join(", ", counts.Select(kv => $"{kv.Key.displayName} x{kv.Value}")));
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "DrinkPanelCheck.png");

        int added = 0;
        while (cup.Add(browser.root.children[0].children[0]) && added < 50) added++;
        Debug.Log($"[Check] cup took {added} more whole milks before reporting full");
        cup.Clear();
        Debug.Log($"[Check] after clear: empty={cup.IsEmpty}, serve enabled={cup.serveButton.interactable}");
    }

    static void Click(string name)
    {
        var entries = browser.VisibleEntries();
        int index = entries.FindIndex(n => n.displayName == name);
        if (index < 0)
        {
            Debug.LogError($"[Check] '{name}' not visible in {browser.Current.displayName}: " +
                           string.Join(", ", entries.Select(n => n.displayName)));
            return;
        }
        browser.ClickSlot(index);
        Log("click " + name);
    }

    static void Back()
    {
        browser.Back();
        Log("back");
    }

    static void Log(string action)
    {
        Debug.Log($"[Check] {action,-22} path='{browser.pathText.text}' slots=[" +
                  string.Join(" | ", browser.slotLabels.Select((l, i) =>
                      (browser.slots[i].interactable ? "" : "-") + l.text.Split('\n')[0])) +
                  $"] back={(browser.backButton.interactable ? "on" : "off")}");
    }
}
