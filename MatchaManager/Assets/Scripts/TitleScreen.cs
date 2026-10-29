using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shown over the dimmed shop at startup. CONTINUE picks up the save (and shows
// which day, how many coins, and the difficulty). NEW GAME opens the difficulty
// page: EASY / MEDIUM / HARD start day 1, BACK returns. If there's a save,
// NEW GAME asks for a second click first so it can't be wiped by accident.
public class TitleScreen : MonoBehaviour
{
    public DayCycle day;

    [Header("Main page")]
    public GameObject mainPage;
    public Button continueButton;
    public TMP_Text continueInfo;
    public Button newGameButton;
    public TMP_Text newGameLabel;

    [Header("Difficulty page")]
    public GameObject difficultyPage;
    [Tooltip("Easy, Medium, Hard")]
    public Button[] difficultyButtons;
    public Button backButton;

    [Tooltip("Seconds the \"sure?\" prompt on NEW GAME waits for the second click")]
    public float confirmSeconds = 3;

    float confirmTimer;

    public bool Showing => gameObject.activeSelf;
    public bool ChoosingDifficulty => difficultyPage.activeSelf;

    void Start()
    {
        continueButton.onClick.AddListener(Continue);
        newGameButton.onClick.AddListener(NewGame);
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            int difficulty = i;
            difficultyButtons[i].onClick.AddListener(() => Choose(difficulty));
        }
        backButton.onClick.AddListener(ShowMain);
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
        ShowMain();
    }

    public void ShowMain()
    {
        mainPage.SetActive(true);
        difficultyPage.SetActive(false);
        confirmTimer = 0;
        newGameLabel.text = "NEW GAME";
        bool hasSave = SaveSystem.Exists;
        continueButton.interactable = hasSave;
        if (hasSave)
        {
            var save = SaveSystem.Load();
            string mode = day.difficulties != null && save.difficulty < day.difficulties.Length
                ? day.difficulties[save.difficulty].difficultyName : "";
            continueInfo.text = $"DAY {save.day} - {save.coins} COINS - {mode}";
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
        confirmTimer = 0;
        mainPage.SetActive(false);
        difficultyPage.SetActive(true);
    }

    public void Choose(int difficulty)
    {
        gameObject.SetActive(false);
        day.StartNewGame(difficulty);
    }
}
