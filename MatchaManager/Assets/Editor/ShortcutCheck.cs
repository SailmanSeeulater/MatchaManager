using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

// Headless check for keyboard shortcuts: lists every binding, then plays a whole
// order (title -> drink -> heated danish -> serve) with the fewest keys: SHIFT+line
// jumps (S1 = SHIFT+1), digits that pour and go back HOME, Z to pour again.
// Real key polling needs Play mode, so this does what KeyboardShortcuts does for each key.
// Run: Unity -batchmode -quit -projectPath . -executeMethod ShortcutCheck.Run -captureOut out.png -savePath save.json
public static class ShortcutCheck
{
    static KeyboardShortcuts keys;
    static CustomerManager customers;
    static DrinkBrowser browser;
    static SnackStation snacks;
    static RecipePanel panel;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SaveSystem.PathOverride = MatchaSceneBuilder.Arg("-savePath") ?? "Temp/ShortcutCheckSave.json";
        SaveSystem.Delete();
        keys = Object.FindAnyObjectByType<KeyboardShortcuts>();
        customers = Object.FindAnyObjectByType<CustomerManager>();
        browser = Object.FindAnyObjectByType<DrinkBrowser>();
        snacks = Object.FindAnyObjectByType<SnackStation>();
        panel = keys.recipePanel;
        var day = Object.FindAnyObjectByType<DayCycle>();

        foreach (var b in keys.game)
            Debug.Log($"[Check] bind {b.key}{(b.altKey != Key.None ? "/" + b.altKey : "")} -> {b.button.name}");
        Debug.Log($"[Check] {keys.hints.Length} key hints");

        // what Play mode would do on startup: hook up every button
        Call(customers.cup, "Awake");
        foreach (var mb in new MonoBehaviour[] { browser, snacks, panel, keys.title, day }) Call(mb, "Start");
        customers.Init();
        customers.autoArrive = false;

        // ENTER on the title (no save yet, so it means NEW GAME)
        KeyboardShortcuts.Press(keys.continueButton.interactable ? keys.continueButton : keys.newGameButton);
        KeyboardShortcuts.Press(keys.title.difficultyButtons[1]); // 2 = medium
        Debug.Log($"[Check] ENTER on title -> title showing={keys.title.Showing} running={day.Running}");

        var sunday = customers.recipes.First(r => r.displayName == "Sunday Matcha");
        var danish = snacks.Menu.First(s => s.needsHeating);
        var order = new Order(sunday, danish);
        customers.Arrive(order);
        customers.Tick(1);
        Debug.Log($"[Check] order needs {order.KeyPresses()} presses, waits {customers.PatienceSeconds(order):0}s; recipe: {panel.body.text.Replace("\n", " / ")}");

        Type("S1"); // ANY MATCHA opens the Matcha folder
        Type("1");  // low-grade pours and goes back HOME
        Type("S2"); // HOT WATER pours straight away
        Type("S3 1 S4 1 1");
        Debug.Log($"[Check] cup: {customers.cup.readout.text}");
        Type("S5 R"); // the danish, into the microwave
        snacks.Tick(5);
        Debug.Log($"[Check] microwave after 5s: '{snacks.appliances[0].timerText.text}'");
        Type("R");
        Debug.Log($"[Check] tray: {snacks.TraySnack?.displayName} {snacks.TrayHeat}");
        Type("TAB");
        Debug.Log($"[Check] after TAB: {panel.Current}");
        MatchaSceneBuilder.Capture(MatchaSceneBuilder.Arg("-captureOut") ?? "ShortcutCheck.png");
        Type("TAB");
        Debug.Log($"[Check] after TAB: {panel.Current} :: {panel.body.text.Replace("\n", " / ")}");
        int before = customers.Coins;
        Type("SPACE");
        Debug.Log($"[Check] SPACE -> '{customers.LastReaction?.Replace("\n", " ")}' paid={customers.Coins - before}");

        Type("SPACE");
        Debug.Log($"[Check] SPACE with empty cup -> served today={customers.Today.served}");
        Type("4 2 Z Z");
        Debug.Log($"[Check] before X: {customers.cup.readout.text}");
        Type("X");
        Debug.Log($"[Check] after X: empty={customers.cup.IsEmpty}");
        Type("Q");
        Debug.Log($"[Check] Q -> tray {snacks.TraySnack?.displayName}");
        Type("T");
        Debug.Log($"[Check] T -> tray {(snacks.TraySnack != null ? snacks.TraySnack.displayName : "empty")}");

        SaveSystem.Delete();
        SaveSystem.PathOverride = null;
    }

    // "2 1 ESC" -> press the button bound to each key in turn
    static void Type(string sequence)
    {
        foreach (var name in sequence.Split(' '))
        {
            if (name == "TAB")
            {
                KeyboardShortcuts.Press(panel.Current == RecipePanel.Tab.Menu ? panel.recipeTab : panel.menuTab);
                continue;
            }
            if (name.Length == 2 && name[0] == 'S' && char.IsDigit(name[1]))
            {
                keys.JumpToLine(name[1] - '1');
                continue;
            }
            if (name == "Z")
            {
                browser.PourAgain();
                continue;
            }
            var key = name switch
            {
                "ESC" => Key.Backspace,
                "SPACE" => Key.Space,
                _ when char.IsDigit(name[0]) => Key.Digit1 + (name[0] - '1'),
                _ => (Key)System.Enum.Parse(typeof(Key), name),
            };
            var binding = keys.game.First(b => b.key == key);
            if (System.Array.IndexOf(browser.slots, binding.button) >= 0) browser.PressSlotKey(binding.button);
            else KeyboardShortcuts.Press(binding.button);
        }
        Debug.Log($"[Check] keys '{sequence}' -> path='{browser.pathText.text}'");
    }

    static void Call(MonoBehaviour target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Invoke(target, null);
}
