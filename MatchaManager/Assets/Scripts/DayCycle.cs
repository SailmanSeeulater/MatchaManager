using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The shop's opening hours. The clock runs from opening to closing time over
// realSecondsPerDay; at closing no new customers come in, the last one is
// finished, and the end-of-day summary appears. NEXT DAY starts the next one
// with a clean station and slightly busier, less patient customers.
// Progress is saved at the start and end of every day: quitting mid-day
// replays that day from its start; quitting at the summary resumes the next day.
// Nothing runs until StartGame (called by the title screen, or right away if
// the scene has none).
public class DayCycle : MonoBehaviour
{
    public CustomerManager customers;
    public Cup cup;
    public SnackStation snacks;
    [Tooltip("Easy, Medium, Hard; the one in use is copied into Balance")]
    public Balance[] difficulties;
    public Balance balance;
    [Tooltip("Optional: if set, the day waits for CONTINUE / NEW GAME")]
    public TitleScreen title;

    [Header("Status bar")]
    public TMP_Text dayText;
    public TMP_Text clockText;

    [Header("End of day")]
    public GameObject summary;
    public TMP_Text summaryTitle;
    public TMP_Text summaryBody;
    public Button nextDayButton;


    public int Day { get; private set; } = 1;
    public bool Ended { get; private set; }
    public bool Running { get; private set; }
    public bool IsOpen => minutes < OpenMinutes;

    float minutes; // game minutes since opening
    SaveData save = new SaveData();
    float OpenMinutes => (balance.closeHour - balance.openHour) * 60f;

    void Start()
    {
        nextDayButton.onClick.AddListener(NextDay);
        customers.open = false; // nobody comes in behind the title screen
        if (title == null) StartGame();
    }

    public int Difficulty { get; private set; } = 1;

    // Loads the save (if any) and opens the shop.
    public void StartGame()
    {
        LoadSave();
        BeginDay();
    }

    // Day 1 at the chosen difficulty (0 easy, 1 medium, 2 hard); any old save is replaced.
    public void StartNewGame(int difficulty)
    {
        SaveSystem.Delete();
        LoadSave();
        UseDifficulty(difficulty);
        customers.SetCoins(balance.startingCoins);
        BeginDay();
    }

    void UseDifficulty(int difficulty)
    {
        if (difficulties == null || difficulties.Length == 0) return;
        Difficulty = Mathf.Clamp(difficulty, 0, difficulties.Length - 1);
        save.difficulty = Difficulty;
        balance = customers.balance = difficulties[Difficulty];
        cup.costMultiplier = balance.costMultiplier;
        if (snacks != null)
        {
            snacks.balance = balance;
            snacks.ApplyBalance();
        }
    }

    // Picks up the saved day and coins (or day 1 with nothing, on a first run).
    public void LoadSave()
    {
        save = SaveSystem.Load();
        Day = Mathf.Max(1, save.day);
        customers.SetCoins(save.coins);
        UseDifficulty(save.difficulty);
    }

    void Update() => Tick(Time.deltaTime);

    public void Tick(float dt)
    {
        if (!Running || Ended) return;
        minutes = Mathf.Min(minutes + dt * OpenMinutes / balance.realSecondsPerDay, OpenMinutes);
        customers.open = IsOpen;
        RefreshClock();
        if (!IsOpen && customers.IsEmpty) EndDay();
    }

    public void NextDay()
    {
        Day++;
        BeginDay();
    }

    public void BeginDay()
    {
        minutes = 0;
        Ended = false;
        Running = true;
        summary.SetActive(false);
        // later days: less patience and more customers
        float hardness = balance.Hardness(Day);
        customers.patienceScale = hardness;
        customers.arrivalScale = hardness;
        customers.dailyGoal = balance.Goal(Day);
        customers.day = Day;
        customers.open = true;
        cup.Clear();
        if (snacks != null) snacks.ResetAll();
        customers.StartDay();
        dayText.text = $"DAY {Day}";
        SoundFX.Play(Sfx.DayStart);
        RefreshClock();
        save.day = Day;
        save.coins = customers.Coins;
        SaveSystem.Save(save);
    }

    void EndDay()
    {
        Ended = true;
        var t = customers.Today;
        bool newBest = t.happy > save.bestHappy;
        save.bestHappy = Mathf.Max(save.bestHappy, t.happy);
        save.totalServed += t.served;
        save.totalHappy += t.happy;
        save.day = Day + 1;
        save.coins = customers.Coins;
        SaveSystem.Save(save);

        summaryTitle.text = $"DAY {Day} DONE";
        int goal = balance.Goal(Day);
        string verdict = t.happy >= goal ? "<color=#2e5222>GOAL REACHED!</color>"
            : t.happy >= goal / 2 ? "NOT BAD!" : "TOMORROW WILL BE BETTER";
        int profit = t.earned - t.spent;
        summaryBody.text =
            $"HAPPY<pos=70%>{t.happy} / {goal}\n" +
            $"SERVED / LOST<pos=70%>{t.served} / {t.walkedOut + t.wrong}\n" +
            $"SALES<pos=70%><color=#2e5222>+{t.earned}</color>\n" +
            $"INGREDIENTS<pos=70%><color=#8a2e2c>-{t.spent}</color>\n" +
            $"PROFIT<pos=70%><color=#{(profit >= 0 ? "2e5222" : "8a2e2c")}>{(profit >= 0 ? "+" : "")}{profit}</color>\n" +
            $"COINS<pos=70%>{customers.Coins}\n" +
            $"BEST DAY<pos=70%>{save.bestHappy} HAPPY{(newBest && Day > 1 ? " <color=#2e5222>NEW!</color>" : "")}\n" +
            verdict + $" <color=#8a7a5a>({balance.difficultyName})</color>";
        summary.SetActive(true);
        SoundFX.Play(Sfx.DayEnd);
        Debug.Log($"[Day] Day {Day} done: served {t.served}, happy {t.happy}, wrong {t.wrong}, walked out {t.walkedOut}, earned {t.earned}, spent {t.spent}, profit {profit}");
    }

    // Steps in 10 minutes, like a shop clock: "9:00 AM", "12:30 PM", then "CLOSED".
    void RefreshClock()
    {
        if (!IsOpen)
        {
            clockText.text = "<color=#d9705a>CLOSED</color>";
            return;
        }
        int total = balance.openHour * 60 + Mathf.FloorToInt(minutes / 10) * 10;
        int hour = total / 60 % 24, minute = total % 60;
        int h12 = hour % 12 == 0 ? 12 : hour % 12;
        clockText.text = $"{h12}:{minute:00} {(hour < 12 ? "AM" : "PM")}";
    }
}
