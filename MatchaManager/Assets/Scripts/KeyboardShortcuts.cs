using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Keys that press on-screen buttons, laid out like the screen itself:
//   1-5        drink slots (top to bottom)      BACKSPACE / ESC  back up a folder
//   SPACE      serve                            X                pour the cup out
//   Q W E      cold snacks    R  microwave      T                toss the tray's snack
//   A S D      hot snacks     F  toaster oven   TAB              switch MENU / RECIPE
// On the title: ENTER = continue (or new game without a save), N = new game.
// On the summary: ENTER or SPACE = next day. F1 hides or shows the key hints.
// A key "presses" its button like a mouse click would (with the pressed flash),
// and does nothing while that button is greyed out.
public class KeyboardShortcuts : MonoBehaviour
{
    [Serializable]
    public class Binding
    {
        public Key key;
        public Key altKey = Key.None;
        public Button button;
    }

    [Tooltip("Shortcuts while the shop is open")]
    public Binding[] game;
    public RecipePanel recipePanel;

    [Header("Overlays")]
    public TitleScreen title;
    public Button continueButton;
    public Button newGameButton;
    public GameObject summary;
    public Button nextDayButton;

    [Tooltip("The little key letters drawn on the buttons")]
    public GameObject[] hints;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f1Key.wasPressedThisFrame)
            foreach (var h in hints) h.SetActive(!h.activeSelf);

        if (title != null && title.Showing)
        {
            if (Pressed(kb, Key.Enter, Key.NumpadEnter))
                Press(continueButton.interactable ? continueButton : newGameButton);
            if (kb.nKey.wasPressedThisFrame) Press(newGameButton);
            return;
        }
        if (summary != null && summary.activeSelf)
        {
            if (Pressed(kb, Key.Enter, Key.NumpadEnter) || kb.spaceKey.wasPressedThisFrame) Press(nextDayButton);
            return;
        }

        foreach (var b in game)
            if (Pressed(kb, b.key, b.altKey)) Press(b.button);
        if (kb.tabKey.wasPressedThisFrame && recipePanel != null)
            Press(recipePanel.Current == RecipePanel.Tab.Menu ? recipePanel.recipeTab : recipePanel.menuTab);
    }

    // A clicked button stays "selected", and the UI would then also press it on SPACE/ENTER.
    // Dropping the selection right after every click keeps keys meaning only what's listed above.
    void LateUpdate()
    {
        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
    }

    static bool Pressed(Keyboard kb, Key key, Key alt) =>
        (key != Key.None && kb[key].wasPressedThisFrame) || (alt != Key.None && kb[alt].wasPressedThisFrame);

    // Same as clicking it: ignored when greyed out or hidden, and flashes the pressed color.
    public static void Press(Button button)
    {
        if (button == null || !button.isActiveAndEnabled || !button.interactable) return;
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }
}
