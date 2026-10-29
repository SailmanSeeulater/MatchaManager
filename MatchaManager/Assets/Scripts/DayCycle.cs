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

    [Header("Hours")]
    public int openHour = 9;
    public int closeHour = 17;
    [Tooltip("Real seconds from opening to closing")]
    public float realSecondsPerDay = 300;

    [Header("Difficulty")]
    [Tooltip("Customers are this much less patient, and come this much more often, each day (0.05 = 5%)")]
    public float patienceDropPerDay = 0.05f;
    [Tooltip("Patience never drops below this fraction of day 1")]
    public float minPatience = 0.6f;
    [Tooltip("Happy customers to aim for each day (the dots in the status bar)")]
    public int dailyGoal = 10;

    public int Day { get; private set; } = 1;
    public bool Ended { get; private set; }
    public bool Running { get; private set; }
    public bool IsOpen => minutes < OpenMinutes;

    float minutes; // game minutes since opening
    SaveData save = new SaveData();
    float OpenMinutes => (closeHour - openHour) * 60f;

    void Start()
    {
        nextDayButton.onClick.AddListener(NextDay);
        customers.open = false; // nobody comes in behind the title screen
        if (title == null) StartGame();
    }

    // Loads the save (if any) and opens the shop.
    public void StartGame()
    {
        LoadSave();
        BeginDay();
    }

    // Picks up the saved day and coins (or day 1 with nothing, on a first run).
    public void LoadSave()
    {
        save = SaveSystem.Load();
        Day = Mathf.Max(1, save.day);
        customers.SetCoins(save.coins);
    }

    void Update() => Tick(Time.deltaTime);

    public void Tick(float dt)
    {
        if (!Running || Ended) return;
        minutes = Mathf.Min(minutes + dt * OpenMinutes / realSecondsPerDay, OpenMinutes);
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
        float hardness = Mathf.Max(minPatience, 1 - patienceDropPerDay * (Day - 1));
        customers.patienceScale = hardness;
        customers.arrivalScale = hardness;
        customers.open = true;
        cup.Clear();
        if (snacks != null) snacks.ResetAll();
        customers.StartDay();
        dayText.text = $"DAY {Day}";
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
        string verdict = t.happy >= dailyGoal ? "<color=#2e5222>GOAL REACHED!</color>"
            : t.happy >= dailyGoal / 2 ? "NOT BAD!" : "TOMORROW WILL BE BETTER";
        summaryBody.text =
            $"SERVED<pos=70%>{t.served}\n" +
            $"HAPPY<pos=70%>{t.happy} / {dailyGoal}\n" +
            $"WRONG ORDERS<pos=70%>{t.wrong}\n" +
            $"WALKED OUT<pos=70%>{t.walkedOut}\n" +
            $"EARNED<pos=70%><color=#2e5222>+{t.earned}</color>\n" +
            $"COINS<pos=70%>{customers.Coins}\n" +
            $"BEST DAY<pos=70%>{save.bestHappy} HAPPY{(newBest && Day > 1 ? " <color=#2e5222>NEW!</color>" : "")}\n" +
            verdict;
        summary.SetActive(true);
        Debug.Log($"[Day] Day {Day} done: served {t.served}, happy {t.happy}, wrong {t.wrong}, walked out {t.walkedOut}, earned {t.earned}");
    }

    // Steps in 10 minutes, like a shop clock: "9:00 AM", "12:30 PM", then "CLOSED".
    void RefreshClock()
    {
        if (!IsOpen)
        {
            clockText.text = "<color=#d9705a>CLOSED</color>";
            return;
        }
        int total = openHour * 60 + Mathf.FloorToInt(minutes / 10) * 10;
        int hour = total / 60 % 24, minute = total % 60;
        int h12 = hour % 12 == 0 ? 12 : hour % 12;
        clockText.text = $"{h12}:{minute:00} {(hour < 12 ? "AM" : "PM")}";
    }
}
