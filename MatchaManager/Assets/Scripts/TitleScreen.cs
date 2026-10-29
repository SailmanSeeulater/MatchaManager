using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shown over the dimmed shop at startup. CONTINUE picks up the save (and shows
// which day and how many coins); NEW GAME starts over at day 1. If there's a
// save, NEW GAME asks for a second click first so it can't be wiped by accident.
public class TitleScreen : MonoBehaviour
{
    public DayCycle day;
    public Button continueButton;
    public TMP_Text continueInfo;
    public Button newGameButton;
    public TMP_Text newGameLabel;

    [Tooltip("Seconds the \"sure?\" prompt on NEW GAME waits for the second click")]
    public float confirmSeconds = 3;

    float confirmTimer;

    public bool Showing => gameObject.activeSelf;

    void Start()
    {
        continueButton.onClick.AddListener(Continue);
        newGameButton.onClick.AddListener(NewGame);
        Show();
    }

    void Update()
    {
        if (confirmTimer <= 0) return;
        confirmTimer -= Time.deltaTime;
        if (confirmTimer <= 0) newGameLabel.text = "NEW GAME";
    }

    public void Show()
    {
        gameObject.SetActive(true);
        confirmTimer = 0;
        newGameLabel.text = "NEW GAME";
        bool hasSave = SaveSystem.Exists;
        continueButton.interactable = hasSave;
        if (hasSave)
        {
            var save = SaveSystem.Load();
            continueInfo.text = $"DAY {save.day}  -  {save.coins} COINS";
        }
        else continueInfo.text = "NO SAVE YET";
    }

    public void Continue()
    {
        gameObject.SetActive(false);
        day.StartGame();
    }

    public void NewGame()
    {
        if (SaveSystem.Exists && confirmTimer <= 0)
        {
            confirmTimer = confirmSeconds;
            newGameLabel.text = "<color=#8a2e2c>SURE? CLICK AGAIN</color>";
            return;
        }
        SaveSystem.Delete();
        gameObject.SetActive(false);
        day.StartGame();
    }
}
