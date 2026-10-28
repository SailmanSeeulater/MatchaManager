using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One customer at a time: they fade in, order a drink (shown in the bubble and
// the recipe panel), wait while the patience bar drains, then react to what
// they're served and leave. Serving hands over the cup, plus the tray's snack
// if they ordered one. Correct orders pay the price plus a patience tip.
// DayCycle closes the door at the end of the day and resets the daily stats.
public class CustomerManager : MonoBehaviour
{
    public class DayStats
    {
        public int served, happy, wrong, walkedOut, earned;
    }

    public List<DrinkRecipe> recipes = new List<DrinkRecipe>();
    public Cup cup;
    public SnackStation snacks;

    [Header("Shop")]
    public CanvasGroup customer;
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
    public Vector2 gapBetweenCustomers = new Vector2(1.5f, 3.5f);

    [Header("Set by DayCycle")]
    public bool open = true;          // new customers only come while open
    public float patienceScale = 1;   // < 1 = customers give up sooner

    enum State { Away, Arriving, Waiting, Reacting, Leaving }
    State state;
    float timer;
    float patience; // 1 = full, 0 = gives up

    public Order CurrentOrder { get; private set; }
    public int Coins { get; private set; }
    public DayStats Today { get; private set; } = new DayStats();
    public bool IsAway => state == State.Away;

    static readonly Color PatienceLow = new Color(1f, 0.55f, 0.5f);

    void Start() => Init();
    void Update() => Tick(Time.deltaTime);

    public void Init()
    {
        cup.Served += OnServed;
        cup.Changed += RefreshRecipe;
        if (snacks != null) snacks.Changed += RefreshRecipe;
        if (recipes.Count == 0) Debug.LogWarning("[Customers] No recipes: run Matcha > Import Drink Tree, then rebuild.");
        GoAway(1f);
        RefreshStatus();
    }

    // Fresh daily stats; the first customer shows up a second later.
    public void StartDay()
    {
        Today = new DayStats();
        GoAway(1f);
        RefreshStatus();
    }

    // Advances the customer's state; Update calls this every frame.
    public void Tick(float dt)
    {
        timer -= dt;
        switch (state)
        {
            case State.Away:
                if (timer <= 0 && open && recipes.Count > 0) Arrive(Order.Random(recipes[Random.Range(0, recipes.Count)], Menu));
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

    public void Arrive(Order order)
    {
        CurrentOrder = order;
        state = State.Arriving;
        timer = fadeTime;
        orderUI.alpha = 0;
    }

    void StartWaiting()
    {
        state = State.Waiting;
        customer.alpha = orderUI.alpha = 1;
        patience = 1;
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
            return;
        }
        var snackProblem = CurrentOrder.SnackProblem(got, heat);
        if (snackProblem != null)
        {
            Today.wrong++;
            Debug.Log($"[Customers] Snack problem: wanted {CurrentOrder.snack.displayName}, got {(got != null ? got.displayName : "nothing")} ({heat})");
            React(snackProblem, "8a2e2c");
            return;
        }
        int tip = Mathf.RoundToInt(CurrentOrder.Price * patience);
        int paid = CurrentOrder.Price + tip;
        Coins += paid;
        Today.happy++;
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
