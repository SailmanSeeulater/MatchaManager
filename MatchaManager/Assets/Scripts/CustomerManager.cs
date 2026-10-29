using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// Customers flow line -> counter -> pickup, like a real café:
// - They walk in every so often and wait in line (up to lineViews.Length, behind the counter).
// - When a pickup spot is free, the next one steps up to the counter and orders
//   (shown in the big bubble), then moves to pickup with a ticket over their head.
// - Up to pickup.Length people wait for their drink at once, each losing patience.
// - SERVE gives the cup (and the tray's snack, if they wanted one) to the
//   longest-waiting customer whose order it matches. A drink nobody ordered
//   stays in the cup. Correct orders pay the price plus a patience tip.
// - One ticket is "selected": its checklist is on the recipe panel. Click a
//   ticket or use the arrow keys to switch.
// DayCycle closes the door at the end of the day and resets the daily stats.
public class CustomerManager : MonoBehaviour
{
    public class DayStats
    {
        public int served, happy, wrong, walkedOut, earned;
    }

    public enum Stage { InLine, Ordering, Waiting, Leaving }

    // One customer: what they want, how patient they still are, what they look like.
    public class Patron
    {
        public Order order;
        public float patience = 1;
        public Sprite look;
        public Stage stage;
        public int spot = -1;      // pickup spot while Waiting / Leaving
        public float timer;        // Ordering: time at the counter; Leaving: time left
        public float ticketTime;   // when they ordered (oldest gets served first)
    }

    [Serializable]
    public class PickupSpot
    {
        public CanvasGroup group;          // customer + ticket, faded together
        public Image view;
        public Button ticket;
        public TMP_Text ticketText;
        public RectTransform patienceFill; // shrinks as patience runs out
        [HideInInspector] public float patienceWidth;
    }

    public List<DrinkRecipe> recipes = new List<DrinkRecipe>();
    public Cup cup;
    public SnackStation snacks;
    public RecipePanel recipePanel;

    [Header("Counter")]
    public CanvasGroup customer;     // the one ordering
    public CanvasGroup orderUI;      // the big order bubble
    public TMP_Text orderText;

    [Header("Pickup")]
    public PickupSpot[] pickup;

    [Header("Status bar")]
    public TMP_Text coinsText;
    public TMP_Text servedText;
    [Tooltip("One dot lights up per happy customer today")]
    public GameObject[] servedDots;

    [Header("Timing (seconds)")]
    public float fadeTime = 0.4f;
    [Tooltip("How long the order bubble shows at the counter before they move to pickup")]
    public float orderTime = 2f;
    public float reactTime = 1.6f;
    [Tooltip("Pause before the next in line steps up to the counter")]
    public Vector2 stepUpDelay = new Vector2(0.6f, 1.2f);

    [Header("Line")]
    [Tooltip("Spots in line, nearest the counter first")]
    public Image[] lineViews;
    public Sprite[] looks;
    [Tooltip("Seconds between new customers walking in")]
    public Vector2 arrivalInterval = new Vector2(8, 16);
    [Tooltip("How fast patience drains in line compared to at pickup")]
    public float linePatienceRate = 0.4f;
    [NonSerialized] public bool autoArrive = true; // the headless checks turn this off

    [Header("Set by DayCycle")]
    public bool open = true;          // new customers only come while open
    public float patienceScale = 1;   // < 1 = customers give up sooner
    public float arrivalScale = 1;    // < 1 = customers come more often

    public int Coins { get; private set; }
    public DayStats Today { get; private set; } = new DayStats();
    public string LastReaction { get; private set; }

    readonly List<Patron> line = new List<Patron>();
    Patron atCounter;
    Patron[] waiting;
    int selected = -1;
    float arrivalTimer, stepTimer, clock;

    static readonly Color LineTint = new Color(0.72f, 0.72f, 0.8f);   // further back, behind the counter
    static readonly Color Impatient = new Color(1f, 0.6f, 0.55f);
    static readonly Color TicketSelected = new Color(1f, 0.92f, 0.55f);
    static readonly Color PatienceOk = new Color32(0x6f, 0xa0, 0x4a, 0xff);
    static readonly Color PatienceLow = new Color32(0xd9, 0x70, 0x5a, 0xff);

    // Nobody in line, at the counter, or at pickup.
    public bool IsEmpty => atCounter == null && line.Count == 0 && waiting.All(p => p == null);
    public int LineLength => line.Count;
    public bool SomeoneOrdering => atCounter != null;
    public int WaitingCount => waiting.Count(p => p != null && p.stage == Stage.Waiting);
    public Patron Selected => selected >= 0 ? waiting[selected] : null;
    public Order CurrentOrder => Selected?.order;
    public float SelectedPatience => Selected?.patience ?? 0;

    void Awake() => waiting = new Patron[pickup.Length];
    void Start() => Init();
    void Update() => Tick(Time.deltaTime);

    public void Init()
    {
        if (waiting == null) waiting = new Patron[pickup.Length];
        cup.Recipient = TryServe;
        cup.Changed += RefreshRecipe;
        if (snacks != null) snacks.Changed += RefreshRecipe;
        for (int i = 0; i < pickup.Length; i++)
        {
            int spot = i;
            pickup[i].patienceWidth = pickup[i].patienceFill.sizeDelta.x;
            pickup[i].ticket.onClick.AddListener(() => Select(spot));
        }
        if (recipes.Count == 0) Debug.LogWarning("[Customers] No recipes: run Matcha > Import Drink Tree, then rebuild.");
        StartDay();
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
        atCounter = null;
        for (int i = 0; i < waiting.Length; i++) waiting[i] = null;
        selected = -1;
        arrivalTimer = 1f;
        stepTimer = 0.5f;
        customer.alpha = orderUI.alpha = 0;
        RefreshAll();
    }

    // Advances everyone; Update calls this every frame.
    public void Tick(float dt)
    {
        clock += dt;
        TickArrivals(dt);
        TickLine(dt);
        TickCounter(dt);
        TickPickup(dt);
    }

    // ---- line --------------------------------------------------------------

    void TickArrivals(float dt)
    {
        if (!autoArrive || !open || recipes.Count == 0) return;
        arrivalTimer -= dt;
        if (arrivalTimer > 0) return;
        arrivalTimer = Random.Range(arrivalInterval.x, arrivalInterval.y) * arrivalScale;
        if (line.Count < lineViews.Length)
        {
            line.Add(NewPatron());
            SoundFX.Play(Sfx.Arrive);
        }
        else Debug.Log("[Customers] Line is full; someone saw it and kept walking");
        RefreshLine();
    }

    // People in line slowly lose patience.
    void TickLine(float dt)
    {
        bool changed = false;
        for (int i = line.Count - 1; i >= 0; i--)
        {
            var p = line[i];
            p.patience -= dt * linePatienceRate / (p.order.PatienceSeconds * patienceScale);
            if (p.patience > 0) continue;
            line.RemoveAt(i);
            Today.walkedOut++;
            changed = true;
            Debug.Log("[Customers] Gave up waiting in line");
            SoundFX.Play(Sfx.WalkOut);
        }
        if (changed) RefreshStatus();
        RefreshLine();
    }

    Patron NewPatron() => new Patron
    {
        order = Order.Random(recipes[Random.Range(0, recipes.Count)], Menu),
        look = looks != null && looks.Length > 0 ? looks[Random.Range(0, looks.Length)] : null,
    };

    // ---- counter -----------------------------------------------------------

    void TickCounter(float dt)
    {
        if (atCounter == null)
        {
            stepTimer -= dt;
            // only step up when there's somewhere to wait afterwards
            if (stepTimer > 0 || line.Count == 0 || FreeSpot() < 0) return;
            atCounter = line[0];
            line.RemoveAt(0);
            atCounter.stage = Stage.Ordering;
            atCounter.timer = 0;
            ShowLook(customer.GetComponent<Image>(), atCounter.look);
            orderText.text = OrderLines(atCounter.order);
            RefreshLine();
            RefreshRecipe();
            return;
        }

        atCounter.timer += dt;
        customer.alpha = orderUI.alpha = Mathf.Clamp01(atCounter.timer / fadeTime);
        if (atCounter.timer < fadeTime + orderTime) return;
        int spot = FreeSpot();
        if (spot < 0) return; // pickup filled up meanwhile; keep ordering until a spot frees up
        var p = atCounter;
        atCounter = null;
        customer.alpha = orderUI.alpha = 0;
        stepTimer = Random.Range(stepUpDelay.x, stepUpDelay.y);
        TakeOrder(p, spot);
    }

    // e.g. "SUNDAY MATCHA / NO MILK / + DANISH", requests colored
    static string OrderLines(Order order)
    {
        var lines = new List<string> { order.recipe.displayName.ToUpper() };
        lines.AddRange(order.requests.Select(r => $"<color=#{(r.StartsWith("NO ") ? "8a2e2c" : "2e5222")}>{r}</color>"));
        if (order.snack != null) lines.Add("+ " + order.snack.displayName.ToUpper());
        return string.Join("\n", lines);
    }

    // ---- pickup ------------------------------------------------------------

    int FreeSpot() => Array.IndexOf(waiting, null);

    // Puts someone straight at pickup with full patience (used by the headless checks).
    public Patron Arrive(Order order)
    {
        int spot = FreeSpot();
        if (spot < 0) return null;
        var p = new Patron { order = order, look = looks != null && looks.Length > 0 ? looks[0] : null };
        TakeOrder(p, spot);
        return p;
    }

    void TakeOrder(Patron p, int spot)
    {
        p.stage = Stage.Waiting;
        p.spot = spot;
        p.ticketTime = clock;
        waiting[spot] = p;
        var s = pickup[spot];
        ShowLook(s.view, p.look);
        // the ticket only has room for the name and snack; "!" means there are changes to check
        s.ticketText.text = (p.order.requests.Count > 0 ? "<color=#8a2e2c>!</color> " : "") +
                            p.order.recipe.displayName.ToUpper() +
                            (p.order.snack != null ? "\n<color=#2e5222>+" + p.order.snack.TicketName.ToUpper() + "</color>" : "");
        s.group.alpha = 1;
        SoundFX.Play(Sfx.Order);
        if (Selected == null || Selected.stage != Stage.Waiting)
        {
            selected = spot;
            recipePanel.ShowTab(RecipePanel.Tab.Recipe);
        }
        RefreshAll();
    }

    void TickPickup(float dt)
    {
        for (int i = 0; i < waiting.Length; i++)
        {
            var p = waiting[i];
            if (p == null) continue;
            var s = pickup[i];
            if (p.stage == Stage.Waiting)
            {
                p.patience -= dt / (p.order.PatienceSeconds * patienceScale);
                RefreshPatience(i);
                if (p.patience <= 0)
                {
                    Today.walkedOut++;
                    React(p, "I'M LEAVING!", "8a2e2c");
                    SoundFX.Play(Sfx.WalkOut);
                }
                continue;
            }
            // Leaving: show the reaction, then fade out and free the spot
            p.timer -= dt;
            s.group.alpha = Mathf.Clamp01(p.timer / fadeTime);
            if (p.timer > 0) continue;
            waiting[i] = null;
            if (selected == i) SelectOldest();
            RefreshAll();
        }
    }

    // Cup.Serve asks this first: returns false when nobody ordered this drink (it stays in the cup).
    bool TryServe(Dictionary<DrinkNode, int> drink)
    {
        var matches = waiting.Where(p => p != null && p.stage == Stage.Waiting && p.order.Check(drink).Correct).ToList();
        if (matches.Count == 0)
        {
            LastReaction = null;
            Debug.Log("[Customers] Nobody ordered that drink");
            return false;
        }
        var traySnack = snacks != null ? snacks.TraySnack : null;
        var trayHeat = snacks != null ? snacks.TrayHeat : Heat.None;
        // among matches, someone the tray's snack is right for, then whoever has waited longest
        var p = matches
            .OrderBy(m => m.order.snack != null && m.order.SnackProblem(traySnack, trayHeat) != null ? 1 : 0)
            .ThenBy(m => m.ticketTime)
            .First();
        // the tray only goes out with orders that include a snack
        var (got, heat) = p.order.snack != null && snacks != null ? snacks.TakeTray() : (null, Heat.None);
        Today.served++;
        var snackProblem = p.order.SnackProblem(got, heat);
        if (snackProblem != null)
        {
            Debug.Log($"[Customers] Snack problem: wanted {p.order.snack.displayName}, got {(got != null ? got.displayName : "nothing")} ({heat})");
            Today.wrong++;
            React(p, snackProblem, "8a2e2c");
            SoundFX.Play(Sfx.ServeBad);
            return true;
        }
        int paid = p.order.Price + Mathf.RoundToInt(p.order.Price * p.patience);
        Coins += paid;
        Today.happy++;
        Today.earned += paid;
        React(p, $"{(p.patience > 0.6f ? "PERFECT!" : "FINALLY...")}\n<color=#2e5222>+{paid}</color>", "1e1a2b");
        SoundFX.Play(Sfx.ServeGood);
        return true;
    }

    void React(Patron p, string message, string color)
    {
        p.stage = Stage.Leaving;
        p.timer = reactTime + fadeTime;
        LastReaction = message;
        pickup[p.spot].ticketText.text = $"<color=#{color}>{message}</color>";
        if (selected == p.spot) SelectOldest();
        RefreshAll();
    }

    // ---- selection ---------------------------------------------------------

    public void Select(int spot)
    {
        if (spot < 0 || spot >= waiting.Length || waiting[spot] == null || waiting[spot].stage != Stage.Waiting) return;
        selected = spot;
        recipePanel.ShowTab(RecipePanel.Tab.Recipe);
        RefreshTickets();
        RefreshRecipe();
    }

    // Arrow keys: step to the next waiting ticket left (-1) or right (+1).
    public void SelectNext(int direction)
    {
        int start = selected < 0 ? 0 : selected;
        for (int k = 1; k <= waiting.Length; k++)
        {
            int i = ((start + direction * k) % waiting.Length + waiting.Length) % waiting.Length;
            if (waiting[i] != null && waiting[i].stage == Stage.Waiting)
            {
                Select(i);
                return;
            }
        }
    }

    void SelectOldest()
    {
        var oldest = waiting.Where(p => p != null && p.stage == Stage.Waiting).OrderBy(p => p.ticketTime).FirstOrDefault();
        selected = oldest != null ? oldest.spot : -1;
    }

    // ---- display -----------------------------------------------------------

    void RefreshAll()
    {
        cup.HasCustomer = WaitingCount > 0;
        for (int i = 0; i < pickup.Length; i++)
            if (waiting[i] == null) pickup[i].group.alpha = 0;
            else RefreshPatience(i);
        RefreshTickets();
        RefreshLine();
        RefreshStatus();
        RefreshRecipe();
    }

    void RefreshTickets()
    {
        for (int i = 0; i < pickup.Length; i++)
            pickup[i].ticket.image.color = i == selected && waiting[i] != null && waiting[i].stage == Stage.Waiting
                ? TicketSelected : Color.white;
    }

    void RefreshPatience(int spot)
    {
        var p = waiting[spot];
        var s = pickup[spot];
        float amount = Mathf.Clamp01(p.patience);
        s.patienceFill.sizeDelta = new Vector2(Mathf.Round(s.patienceWidth * amount), s.patienceFill.sizeDelta.y);
        s.patienceFill.GetComponent<Image>().color = amount < 0.35f ? PatienceLow : PatienceOk;
        s.view.color = amount < 0.35f && p.stage == Stage.Waiting ? Color.Lerp(Color.white, Impatient, 0.5f) : Color.white;
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
            view.color = line[i].patience < 0.35f ? Color.Lerp(LineTint, Impatient, 0.6f) : LineTint;
        }
    }

    // Customer images are pivoted at the feet, so looks of different heights stand on the same spot.
    static void ShowLook(Image view, Sprite look)
    {
        if (look == null || view.sprite == look) return;
        view.sprite = look;
        view.SetNativeSize();
    }

    void RefreshStatus()
    {
        if (coinsText != null) coinsText.text = Coins.ToString();
        if (servedText != null) servedText.text = $"SERVED {Today.served}";
        if (servedDots != null)
            for (int i = 0; i < servedDots.Length; i++) servedDots[i].SetActive(i < Today.happy);
    }

    // Recipe panel: the selected ticket's lines, ticked off as the cup matches them.
    void RefreshRecipe()
    {
        var p = Selected;
        if (p == null || p.stage != Stage.Waiting)
        {
            recipePanel.SetOrder("NO ORDERS YET", atCounter != null ? "ORDERING AT THE COUNTER..." : "WAITING FOR A CUSTOMER...");
            return;
        }
        var order = p.order;
        var result = order.Check(cup.Counts());
        var rows = new List<string>();
        for (int i = 0; i < order.lines.Count; i++)
        {
            var line = order.lines[i];
            if (line.removed) { rows.Add($"[-] <s>{line.Name}</s> NONE"); continue; }
            string count = result.have[i] > 0 && !result.lineOk[i] ? $" ({result.have[i]}/{line.portions})" : "";
            rows.Add($"[{(result.lineOk[i] ? "x" : " ")}] {line.Name} {line.Amount}{count}");
        }
        if (order.snack != null) rows.Add(SnackRow(order));
        if (result.extras.Count > 0)
            rows.Add("<color=#8a2e2c>[!] NOT ORDERED: " +
                     string.Join(", ", result.extras.Select(n => n.displayName.ToUpper())) + "</color>");
        string others = WaitingCount > 1 ? $" <color=#8a7a5a>({WaitingCount} WAITING)</color>" : "";
        recipePanel.SetOrder($"#{p.spot + 1} {order.recipe.displayName.ToUpper()}{others}", string.Join("\n", rows));
    }

    IList<Snack> Menu => snacks != null ? snacks.Menu : new Snack[0];

    // e.g. "[ ] HOT DANISH (COLD)" while the tray has an underheated danish
    string SnackRow(Order order)
    {
        var want = order.snack;
        string name = (want.needsHeating ? "HOT " : "") + want.displayName.ToUpper();
        var got = snacks != null ? snacks.TraySnack : null;
        var heat = snacks != null ? snacks.TrayHeat : Heat.None;
        bool ok = got != null && order.SnackProblem(got, heat) == null;
        string note = got == null || ok ? ""
            : got != want ? $" (TRAY: {got.displayName.ToUpper()})"
            : heat == Heat.Burnt ? " (BURNT)" : " (COLD)";
        return $"[{(ok ? "x" : " ")}] {name}{note}";
    }
}
