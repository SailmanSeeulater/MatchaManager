using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Keys that press on-screen buttons, laid out like the screen itself:
//   1-5        drink slots (top to bottom)      ESC / BACKSPACE / P  back up a folder
//   SPACE      serve                            X                pour the cup out
//   Q W E      cold snacks    R  microwave      T                toss the tray's snack
//   A S D      hot snacks     F  toaster oven   TAB              switch MENU / RECIPE
//   LEFT / RIGHT  switch which waiting customer's ticket the recipe panel shows
//   G          ask an unclear customer what they meant; then 1 / 2 / 3 picks a guess
//              and ESC / BACKSPACE / P closes the guesses
// On the title: ENTER = continue (or new game without a save), N = new game;
// then 1 / 2 / 3 (or E / M / H) picks easy / medium / hard, ESC goes back.
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
        public Key altKey2 = Key.None;
        public Button button;
    }

    [Tooltip("Shortcuts while the shop is open")]
    public Binding[] game;
    public RecipePanel recipePanel;
    public CustomerManager customers;

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
            if (title.ChoosingDifficulty)
            {
                Key[][] keys = { new[] { Key.Digit1, Key.E }, new[] { Key.Digit2, Key.M }, new[] { Key.Digit3, Key.H } };
                for (int i = 0; i < keys.Length && i < title.difficultyButtons.Length; i++)
                    if (Pressed(kb, keys[i][0], keys[i][1])) Press(title.difficultyButtons[i]);
                if (Pressed(kb, Key.Escape, Key.Backspace) || kb.pKey.wasPressedThisFrame) Press(title.backButton);
                return;
            }
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

        // the guess bubbles take 1-3 and the back keys while they're open
        if (customers != null && customers.Guessing)
        {
            Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3 };
            Key[] numpad = { Key.Numpad1, Key.Numpad2, Key.Numpad3 };
            for (int i = 0; i < digits.Length && i < customers.guessButtons.Length; i++)
                if (Pressed(kb, digits[i], numpad[i])) Press(customers.guessButtons[i]);
            if (Pressed(kb, Key.Escape, Key.Backspace) || kb.pKey.wasPressedThisFrame) customers.CloseGuess();
            return;
        }
        if (customers != null && kb.gKey.wasPressedThisFrame) customers.OpenGuess();

        foreach (var b in game)
            if (Pressed(kb, b.key, b.altKey) || (b.altKey2 != Key.None && kb[b.altKey2].wasPressedThisFrame)) Press(b.button);
        if (customers != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame) customers.SelectNext(-1);
            if (kb.rightArrowKey.wasPressedThisFrame) customers.SelectNext(1);
        }
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
