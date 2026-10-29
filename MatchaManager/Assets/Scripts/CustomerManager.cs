using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Customers arrive every so often and wait in line (up to lineViews.Length).
// The one at the counter orders a drink (shown in the bubble and the recipe
// panel), waits while the patience bar drains, then reacts to what they're
// served and leaves; the next in line steps up. People in line lose patience
// too, just more slowly, and walk out if it runs out. Serving hands over the cup, plus the tray's snack
// if they ordered one. Correct orders pay the price plus a patience tip.
// DayCycle closes the door at the end of the day and resets the daily stats.
public class CustomerManager : MonoBehaviour
{
    public class DayStats
    {
        public int served, happy, wrong, walkedOut, earned;
    }

    // One customer: what they want, how patient they still are, what they look like.
    public class Patron
    {
        public Order order;
        public float patience = 1;
        public Sprite look;
    }

    public List<DrinkRecipe> recipes = new List<DrinkRecipe>();
    public Cup cup;
    public SnackStation snacks;

    [Header("Shop")]
    public CanvasGroup customer;     // the one at the counter
    public CanvasGroup orderUI;      // bubble + patience bar
    public TMP_Text orderText;
    public Image patienceFill;

    public RecipePanel recipePanel;

    [Header("Status bar")]
    public TMP_Text coinsText;
    public TMP_Text servedText;
    [Tooltip("One dot lights up per happy customer today")]
    public GameObject[] servedDots;

    [Header("Timing (seconds)")]
    public float fadeTime = 0.4f;
    public float reactTime = 1.6f;
    [Tooltip("Pause before the next in line steps up to the counter")]
    public Vector2 gapBetweenCustomers = new Vector2(0.6f, 1.2f);

    [Header("Line")]
    [Tooltip("Spots in line, nearest the counter first")]
    public Image[] lineViews;
    public Sprite[] looks;
    [Tooltip("Seconds between new customers walking in")]
    public Vector2 arrivalInterval = new Vector2(10, 20);
    [Tooltip("How fast patience drains in line compared to at the counter")]
    public float linePatienceRate = 0.4f;
    [System.NonSerialized] public bool autoArrive = true; // the headless checks turn this off

    [Header("Set by DayCycle")]
    public bool open = true;          // new customers only come while open
    public float patienceScale = 1;   // < 1 = customers give up sooner
    public float arrivalScale = 1;    // < 1 = customers come more often

    enum State { Away, Arriving, Waiting, Reacting, Leaving }
    State state;
    float timer;
    float patience; // 1 = full, 0 = gives up

    public Order CurrentOrder { get; private set; }
    public int Coins { get; private set; }
    public DayStats Today { get; private set; } = new DayStats();
    // Nobody at the counter or in line.
    public bool IsEmpty => state == State.Away && line.Count == 0;
    public int LineLength => line.Count;

    readonly List<Patron> line = new List<Patron>();
    float arrivalTimer;
    static readonly Color LineTint = new Color(0.82f, 0.82f, 0.88f); // a step further back

    static readonly Color PatienceLow = new Color(1f, 0.55f, 0.5f);

    void Start() => Init();
    void Update() => Tick(Time.deltaTime);

    public void Init()
    {
        cup.Served += OnServed;
        cup.Changed += RefreshRecipe;
        if (snacks != null) snacks.Changed += RefreshRecipe;
        if (recipes.Count == 0) Debug.LogWarning("[Customers] No recipes: run Matcha > Import Drink Tree, then rebuild.");
        arrivalTimer = 1f;
        GoAway(0.5f);
        RefreshStatus();
    }

    // Coins from a save file.
    public void SetCoins(int coins)
    {
        Coins = coins;
        RefreshStatus();
    }

    // Fresh daily stats and an empty shop; the first customer walks in a second later.
    public void StartDay()
    {
        Today = new DayStats();
        line.Clear();
        arrivalTimer = 1f;
        GoAway(0.5f);
        RefreshStatus();
    }

    // Advances the customer's state; Update calls this every frame.
    public void Tick(float dt)
    {
        timer -= dt;
        TickLine(dt);
        switch (state)
        {
            case State.Away:
                if (timer <= 0 && line.Count > 0)
                {
                    var next = line[0];
                    line.RemoveAt(0);
                    Arrive(next);
                    RefreshLine();
                }
                break;
            case State.Arriving:
                customer.alpha = Mathf.Clamp01(1 - timer / fadeTime);
                if (timer <= 0) StartWaiting();
                break;
            case State.Waiting:
                patience -= dt / (CurrentOrder.PatienceSeconds * patienceScale);
                RefreshPatience();
                if (patience <= 0)
                {
                    Today.walkedOut++;
                    React("I'M LEAVING!", "8a2e2c");
                    SoundFX.Play(Sfx.WalkOut);
                }
                break;
            case State.Reacting:
                if (timer <= 0) { state = State.Leaving; timer = fadeTime; }
                break;
            case State.Leaving:
                float a = Mathf.Clamp01(timer / fadeTime);
                customer.alpha = orderUI.alpha = a;
                if (timer <= 0) GoAway(Random.Range(gapBetweenCustomers.x, gapBetweenCustomers.y));
                break;
        }
    }

    // New arrivals join the end of the line; people in line slowly lose patience.
    void TickLine(float dt)
    {
        if (autoArrive && open && recipes.Count > 0)
        {
            arrivalTimer -= dt;
            if (arrivalTimer <= 0)
            {
                arrivalTimer = Random.Range(arrivalInterval.x, arrivalInterval.y) * arrivalScale;
                if (line.Count < lineViews.Length)
                {
                    line.Add(NewPatron());
                    SoundFX.Play(Sfx.Arrive);
                }
                else Debug.Log("[Customers] Line is full; someone saw it and kept walking");
            }
        }
        for (int i = line.Count - 1; i >= 0; i--)
        {
            var p = line[i];
            p.patience -= dt * linePatienceRate / (p.order.PatienceSeconds * patienceScale);
            if (p.patience > 0) continue;
            line.RemoveAt(i);
            Today.walkedOut++;
            Debug.Log("[Customers] Gave up waiting in line");
            SoundFX.Play(Sfx.WalkOut);
            RefreshStatus();
        }
        RefreshLine();
    }

    Patron NewPatron() => new Patron
    {
        order = Order.Random(recipes[Random.Range(0, recipes.Count)], Menu),
        look = looks != null && looks.Length > 0 ? looks[Random.Range(0, looks.Length)] : null,
    };

    // Puts someone at the counter straight away (with full patience).
    public void Arrive(Order order) => Arrive(new Patron { order = order, look = looks != null && looks.Length > 0 ? looks[0] : null });

    public void Arrive(Patron patron)
    {
        CurrentOrder = patron.order;
        patience = patron.patience;
        ShowLook(customer.GetComponent<Image>(), patron.look);
        state = State.Arriving;
        timer = fadeTime;
        orderUI.alpha = 0;
    }

    void StartWaiting()
    {
        state = State.Waiting;
        customer.alpha = orderUI.alpha = 1;
        RefreshPatience();
        var lines = new List<string> { CurrentOrder.recipe.displayName.ToUpper() };
        lines.AddRange(CurrentOrder.requests.Select(r =>
            $"<color=#{(r.StartsWith("NO ") ? "8a2e2c" : "2e5222")}>{r}</color>"));
        if (CurrentOrder.snack != null) lines.Add("+ " + CurrentOrder.snack.displayName.ToUpper());
        orderText.text = string.Join("\n", lines);
        cup.HasCustomer = true;
        recipePanel.ShowTab(RecipePanel.Tab.Recipe);
        RefreshRecipe();
    }

    void OnServed(Dictionary<DrinkNode, int> drink)
    {
        if (state != State.Waiting) return;
        var result = CurrentOrder.Check(drink);
        // the tray only goes out with orders that include a snack
        var (got, heat) = CurrentOrder.snack != null && snacks != null ? snacks.TakeTray() : (null, Heat.None);
        Today.served++;
        if (!result.Correct)
        {
            Today.wrong++;
            Debug.Log("[Customers] Wrong drink: " + Explain(result));
            React("THAT'S NOT\nWHAT I ORDERED", "8a2e2c");
            SoundFX.Play(Sfx.ServeBad);
            return;
        }
        var snackProblem = CurrentOrder.SnackProblem(got, heat);
        if (snackProblem != null)
        {
            Today.wrong++;
            Debug.Log($"[Customers] Snack problem: wanted {CurrentOrder.snack.displayName}, got {(got != null ? got.displayName : "nothing")} ({heat})");
            React(snackProblem, "8a2e2c");
            SoundFX.Play(Sfx.ServeBad);
            return;
        }
        int tip = Mathf.RoundToInt(CurrentOrder.Price * patience);
        int paid = CurrentOrder.Price + tip;
        Coins += paid;
        Today.happy++;
        SoundFX.Play(Sfx.ServeGood);
        Today.earned += paid;
        React($"{(patience > 0.6f ? "PERFECT!" : "FINALLY...\nTHANKS")}\n<color=#2e5222>+{paid}</color>", "1e1a2b");
    }

    void React(string message, string color)
    {
        state = State.Reacting;
        timer = reactTime;
        orderText.text = $"<color=#{color}>{message}</color>";
        cup.HasCustomer = false;
        RefreshStatus();
    }

    void GoAway(float wait)
    {
        state = State.Away;
        timer = wait;
        CurrentOrder = null;
        customer.alpha = orderUI.alpha = 0;
        cup.HasCustomer = false;
        RefreshRecipe();
    }

    void RefreshLine()
    {
        if (lineViews == null) return;
        for (int i = 0; i < lineViews.Length; i++)
        {
            var view = lineViews[i];
            view.enabled = i < line.Count;
            if (!view.enabled) continue;
            ShowLook(view, line[i].look);
            // getting impatient: they go a bit red
            view.color = line[i].patience < 0.35f ? Color.Lerp(LineTint, new Color(1f, 0.6f, 0.55f), 0.6f) : LineTint;
        }
    }

    // Customer images are pivoted at the feet, so looks of different heights stand on the same spot.
    static void ShowLook(Image view, Sprite look)
    {
        if (look == null || view.sprite == look) return;
        view.sprite = look;
        view.SetNativeSize();
    }

    void RefreshPatience()
    {
        patienceFill.fillAmount = Mathf.Clamp01(patience);
        patienceFill.color = patience < 0.35f ? PatienceLow : Color.white;
    }

    void RefreshStatus()
    {
        if (coinsText != null) coinsText.text = Coins.ToString();
        if (servedText != null) servedText.text = $"SERVED {Today.served}";
        if (servedDots != null)
            for (int i = 0; i < servedDots.Length; i++) servedDots[i].SetActive(i < Today.happy);
    }

    // Recipe panel: the current order's lines, ticked off as the cup matches them.
    void RefreshRecipe()
    {
        if (CurrentOrder == null || state == State.Away || state == State.Arriving)
        {
            recipePanel.SetOrder("NO ORDER YET", "WAITING FOR A CUSTOMER...");
            return;
        }
        var result = CurrentOrder.Check(cup.Counts());
        var rows = new List<string>();
        for (int i = 0; i < CurrentOrder.lines.Count; i++)
        {
            var line = CurrentOrder.lines[i];
            if (line.removed) { rows.Add($"[-] <s>{line.Name}</s> NONE"); continue; }
            string count = result.have[i] > 0 && !result.lineOk[i] ? $" ({result.have[i]}/{line.portions})" : "";
            rows.Add($"[{(result.lineOk[i] ? "x" : " ")}] {line.Name} {line.Amount}{count}");
        }
        if (CurrentOrder.snack != null) rows.Add(SnackRow());
        if (result.extras.Count > 0)
            rows.Add("<color=#8a2e2c>[!] NOT ORDERED: " +
                     string.Join(", ", result.extras.Select(n => n.displayName.ToUpper())) + "</color>");
        recipePanel.SetOrder(CurrentOrder.recipe.displayName.ToUpper(), string.Join("\n", rows));
    }

    IList<Snack> Menu => snacks != null ? snacks.Menu : new Snack[0];

    // e.g. "[ ] HOT DANISH (COLD)" while the tray has an underheated danish
    string SnackRow()
    {
        var want = CurrentOrder.snack;
        string name = (want.needsHeating ? "HOT " : "") + want.displayName.ToUpper();
        var got = snacks != null ? snacks.TraySnack : null;
        var heat = snacks != null ? snacks.TrayHeat : Heat.None;
        bool ok = got != null && CurrentOrder.SnackProblem(got, heat) == null;
        string note = got == null || ok ? ""
            : got != want ? $" (TRAY: {got.displayName.ToUpper()})"
            : heat == Heat.Burnt ? " (BURNT)" : " (COLD)";
        return $"[{(ok ? "x" : " ")}] {name}{note}";
    }

    static string Explain(Order.Result r) =>
        $"lines ok=[{string.Join(",", r.lineOk)}] have=[{string.Join(",", r.have)}] extras=[{string.Join(",", r.extras.Select(n => n.displayName))}]";
}
