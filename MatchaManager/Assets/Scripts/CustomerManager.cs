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
// - On Medium and Hard some customers mumble ("??? MATCHA"): name and changes stay
//   hidden. Click their ticket (or G) to ask: 3 guess bubbles appear. A right guess
//   reveals the order; a wrong one costs everyone waiting some patience. Or watch: a few
//   clear letters slide along the mumbled order (name and changes) on the ticket and the
//   recipe panel. Making the right drink without asking earns a bonus that shrinks as
//   more of the order has been shown, so recognising it early pays most.
// - Ingredients and snacks cost coins as you use them. Customers can't taste the
//   difference, so on ANY lines the cheapest choice is the most profit, unless the
//   order asks for a particular one ("WITH CEREMONIAL MATCHA").
// DayCycle closes the door at the end of the day and resets the daily stats.
public class CustomerManager : MonoBehaviour
{
    public class DayStats
    {
        public int served, happy, wrong, walkedOut, earned;
        public int spent;      // ingredients and snacks; profit = earned - spent
        public int turnedAway; // saw the line was full and kept walking
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
        public List<DrinkRecipe> guesses;              // unclear orders: the 3 names to pick from
        public HashSet<int> wrongGuesses = new HashSet<int>();
        public float peekTime;     // unclear orders: seconds the peek has been sliding
        public int peekStep = -1;  // which letter the peek has reached this sweep
        public float seen;         // share of the mumble the peek has shown (0-1); shrinks the bonus
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

    public Balance balance;
    public List<DrinkRecipe> recipes = new List<DrinkRecipe>();
    [Tooltip("The drink tree root, for picking what orders can ADD")]
    public DrinkNode drinkTree;
    public Cup cup;
    public SnackStation snacks;
    public RecipePanel recipePanel;

    [Header("Counter")]
    public CanvasGroup customer;     // the one ordering
    public CanvasGroup orderUI;      // the big order bubble
    public TMP_Text orderText;

    [Header("Pickup")]
    public PickupSpot[] pickup;

    [Header("Guessing unclear orders")]
    public RectTransform guessPopup;
    public Button[] guessButtons;
    public TMP_Text[] guessLabels;
    [Tooltip("Top of the guess bubbles, in shop pixels")]
    public float guessTop = 58;

    [Header("Status bar")]
    public TMP_Text coinsText;
    public TMP_Text servedText;
    [Tooltip("Lights up toward today's goal of happy customers")]
    public GameObject[] servedDots;

    [Header("Timing (seconds)")]
    public float fadeTime = 0.4f;
    public float reactTime = 1.6f;
    [Tooltip("Pause before the next in line steps up to the counter")]
    public Vector2 stepUpDelay = new Vector2(0.6f, 1.2f);

    [Header("Line")]
    [Tooltip("Spots in line, nearest the counter first")]
    public Image[] lineViews;
    public Sprite[] looks;
    [NonSerialized] public bool autoArrive = true; // the headless checks turn this off

    [Header("Set by DayCycle")]
    public bool open = true;          // new customers only come while open
    public float patienceScale = 1;   // < 1 = customers give up sooner
    public float arrivalScale = 1;    // < 1 = customers come more often
    public int dailyGoal = 10;        // the served dots fill toward this
    public int day = 1;               // orders get more finicky as days go by

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
    static readonly Color PatienceMid = new Color32(0xe0, 0xb0, 0x50, 0xff);
    static readonly Color PatienceLow = new Color32(0xd9, 0x70, 0x5a, 0xff);

    // Nobody in line, at the counter, or at pickup.
    public bool IsEmpty => atCounter == null && line.Count == 0 && waiting.All(p => p == null);
    public int LineLength => line.Count;
    public bool SomeoneOrdering => atCounter != null;
    public IEnumerable<Patron> WaitingPatrons => (waiting ?? new Patron[0]).Where(p => p != null && p.stage == Stage.Waiting);
    public int WaitingCount => WaitingPatrons.Count();
    public Patron Selected => selected >= 0 ? waiting[selected] : null;
    public Order CurrentOrder => Selected?.order;
    public float SelectedPatience => Selected?.patience ?? 0;
    public bool Guessing => guessPopup != null && guessPopup.gameObject.activeSelf;

    void Awake() => waiting = new Patron[pickup.Length];
    void Start() => Init();
    void Update() => Tick(Time.deltaTime);

    public void Init()
    {
        if (waiting == null) waiting = new Patron[pickup.Length];
        cup.Recipient = TryServe;
        cup.Dumped += DumpPenalty;
        cup.Spent += Spend;
        if (snacks != null) snacks.Spent += Spend;
        cup.Changed += RefreshRecipe;
        if (snacks != null) snacks.Changed += RefreshRecipe;
        for (int i = 0; i < pickup.Length; i++)
        {
            int spot = i;
            pickup[i].patienceWidth = pickup[i].patienceFill.sizeDelta.x;
            pickup[i].ticket.onClick.AddListener(() => ClickTicket(spot));
        }
        for (int i = 0; i < guessButtons.Length; i++)
        {
            int option = i;
            guessButtons[i].onClick.AddListener(() => Guess(option));
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
        CloseGuess();
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
        arrivalTimer = Random.Range(balance.arrivalInterval.x, balance.arrivalInterval.y) * arrivalScale;
        if (line.Count < lineViews.Length)
        {
            line.Add(NewPatron());
            SoundFX.Play(Sfx.Arrive);
        }
        else
        {
            Today.turnedAway++;
            Debug.Log("[Customers] Line is full; someone saw it and kept walking");
        }
        RefreshLine();
    }

    // People in line slowly lose patience.
    void TickLine(float dt)
    {
        bool changed = false;
        for (int i = line.Count - 1; i >= 0; i--)
        {
            var p = line[i];
            p.patience -= dt * balance.linePatienceRate / PatienceSeconds(p.order);
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

    Patron NewPatron()
    {
        var p = new Patron
        {
            order = Order.Random(recipes[Random.Range(0, recipes.Count)], Menu, balance, day, Addable),
            look = looks != null && looks.Length > 0 ? looks[Random.Range(0, looks.Length)] : null,
        };
        if (Random.value < balance.UnclearChance(day)) MakeUnclear(p);
        return p;
    }

    // Ingredients and snacks are paid for as they're used.
    void Spend(int coins)
    {
        if (coins == 0) return;
        Coins -= coins;
        Today.spent += coins;
        RefreshStatus();
    }


    // Only part of the name: a word the drink shares with other drinks, so it really is unclear
    // ("??? MATCHA" could be Sunday Matcha, Iced Matcha or Matcha Latte). Drinks with no shared
    // word stay clear. Also picks the 3 names to guess from: the real one plus lookalikes.
    public bool MakeUnclear(Patron p)
    {
        var recipe = p.order.recipe;
        string[] Words(DrinkRecipe r) => r.displayName.ToUpper().Split(' ');
        var words = Words(recipe);
        var shared = words.Where(w => recipes.Any(r => r != recipe && Words(r).Contains(w))).ToList();
        if (shared.Count == 0) return false;
        var word = shared[Random.Range(0, shared.Count)];
        p.order.unclear = true;
        p.order.clue = string.Join(" ", words.Select(w => w == word ? w : "???")).Replace("??? ???", "???");
        var lookalikes = recipes.Where(r => r != recipe && Words(r).Contains(word)).OrderBy(_ => Random.value);
        var others = recipes.Where(r => r != recipe && !Words(r).Contains(word)).OrderBy(_ => Random.value);
        p.guesses = new[] { recipe }.Concat(lookalikes).Concat(others).Take(3).OrderBy(_ => Random.value).ToList();
        p.wrongGuesses.Clear();
        return true;
    }

    // ---- the mumble peek ---------------------------------------------------

    // Slides the clear letters along; the ticket and recipe panel redraw each time they move.
    void TickPeek(Patron p, float dt)
    {
        p.peekTime += dt;
        string text = p.order.Mumble;
        int cycle = text.Length + balance.peekLetters + Mathf.RoundToInt(balance.peekPause * balance.peekSpeed);
        int total = Mathf.FloorToInt(p.peekTime * balance.peekSpeed);
        int step = total % cycle;
        if (step == p.peekStep) return;
        p.peekStep = step;
        p.seen = Mathf.Max(p.seen, Mathf.Clamp01((total + 1f) / text.Length));
        RefreshTicketText(p);
        if (selected == p.spot) RefreshRecipe();
    }

    // The mumble with only the peek's letters (and the clue word) readable, e.g. "S?N??? MATCHA, ?? ????".
    // nameOnly: just the drink name, for the small ticket.
    string Peek(Patron p, bool nameOnly)
    {
        string name = p.order.recipe.displayName.ToUpper();
        string text = nameOnly ? name : p.order.Mumble;
        var clueWords = new HashSet<string>(p.order.clue.Split(' ').Where(w => w != "???"));
        int last = p.peekStep, first = p.peekStep - balance.peekLetters + 1;
        var sb = new System.Text.StringBuilder();
        bool open = false;
        int wordStart = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (i == 0 || !char.IsLetterOrDigit(text[i - 1])) wordStart = i;
            bool inPeek = p.peekStep >= 0 && i >= first && i <= last;
            if (inPeek != open)
            {
                sb.Append(inPeek ? "<color=#b8423f>" : "</color>");
                open = inPeek;
            }
            if (!char.IsLetterOrDigit(c) || inPeek) { sb.Append(c); continue; }
            int wordEnd = i;
            while (wordEnd < text.Length && char.IsLetterOrDigit(text[wordEnd])) wordEnd++;
            bool clue = i < name.Length && clueWords.Contains(text.Substring(wordStart, wordEnd - wordStart));
            sb.Append(clue ? c : '?');
        }
        if (open) sb.Append("</color>");
        return sb.ToString();
    }

    // The bonus for serving this unclear order right now, without asking.
    public int GuessBonusNow(Patron p) => Mathf.RoundToInt(p.order.Price * balance.guessBonus * (1 - p.seen));

    // How long this order's customer waits at pickup before giving up (hot snacks take a while).
    public float PatienceSeconds(Order order) =>
        (balance.patienceBase + order.KeyPresses() * balance.secondsPerPress +
         (order.snack != null && order.snack.needsHeating && snacks != null ? snacks.appliances.Min(a => a.readySeconds) : 0))
        * patienceScale;

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
        if (atCounter.timer < fadeTime + balance.orderSeconds) return;
        int spot = FreeSpot();
        if (spot < 0) return; // pickup filled up meanwhile; keep ordering until a spot frees up
        var p = atCounter;
        atCounter = null;
        customer.alpha = orderUI.alpha = 0;
        stepTimer = Random.Range(stepUpDelay.x, stepUpDelay.y);
        TakeOrder(p, spot);
    }

    // Small ingredients an order may ADD (sweeteners, syrups, ice), from Balance.addMaxPixels.
    List<DrinkNode> addable;
    List<DrinkNode> Addable => addable ??= drinkTree == null ? new List<DrinkNode>()
        : drinkTree.Ingredients().Where(n => n.cupPixels <= balance.addMaxPixels).ToList();

    // Dumping the cup makes everyone waiting a bit less patient.
    public void DumpPenalty()
    {
        float penalty = balance.dumpPenalty;
        foreach (var p in WaitingPatrons) p.patience -= penalty;
        foreach (var p in line) p.patience -= penalty;
        Debug.Log($"[Customers] Cup dumped: everyone waiting lost {penalty:P0} patience");
        RefreshAll();
    }

    // e.g. "SUNDAY MATCHA / NO MILK / + DANISH", requests colored; long lists are cut short
    // (the recipe panel always has every change). Unclear orders only show the clue.
    static string OrderLines(Order order)
    {
        const int shown = 3;
        if (order.unclear)
            return order.clue + "\n<color=#8a7a5a>(MUMBLES)</color>" + (order.snack != null ? "\n+ " + order.snack.displayName.ToUpper() : "");
        var lines = new List<string> { order.recipe.displayName.ToUpper() };
        lines.AddRange(order.requests.Take(order.requests.Count > shown ? shown - 1 : shown)
            .Select(r => $"<color=#{(r.StartsWith("NO ") ? "8a2e2c" : "2e5222")}>{r}</color>"));
        if (order.requests.Count > shown) lines.Add($"<color=#8a2e2c>+{order.requests.Count - shown + 1} MORE</color>");
        if (order.snack != null) lines.Add("+ " + order.snack.displayName.ToUpper());
        return string.Join("\n", lines);
    }

    // ---- pickup ------------------------------------------------------------

    int FreeSpot() => Array.IndexOf(waiting, null);

    // Puts someone straight at pickup with full patience (used by the headless checks).
    public Patron Arrive(Order order, bool unclear = false)
    {
        int spot = FreeSpot();
        if (spot < 0) return null;
        var p = new Patron { order = order, look = looks != null && looks.Length > 0 ? looks[0] : null };
        if (unclear) MakeUnclear(p);
        TakeOrder(p, spot);
        return p;
    }

    void TakeOrder(Patron p, int spot)
    {
        p.stage = Stage.Waiting;
        p.spot = spot;
        p.ticketTime = clock;
        waiting[spot] = p;
        ShowLook(pickup[spot].view, p.look);
        RefreshTicketText(p);
        pickup[spot].group.alpha = 1;
        SoundFX.Play(Sfx.Order);
        if (Selected == null || Selected.stage != Stage.Waiting)
        {
            selected = spot;
            recipePanel.ShowTab(RecipePanel.Tab.Recipe);
        }
        RefreshAll();
    }

    // The ticket only has room for the name, then "!" (changes to check on the recipe panel)
    // and the snack. Unclear orders show the clue and "? ASK G".
    void RefreshTicketText(Patron p)
    {
        if (p.order.unclear)
        {
            pickup[p.spot].ticketText.text = Peek(p, true) + "\n<color=#8a2e2c>? ASK G</color>";
            return;
        }
        string extras = p.order.unclear ? "<color=#8a2e2c>? ASK G</color>"
            : (p.order.requests.Count > 0 ? "<color=#8a2e2c>!</color> " : "") +
              (p.order.snack != null ? "<color=#2e5222>+" + p.order.snack.TicketName.ToUpper() + "</color>" : "");
        pickup[p.spot].ticketText.text = p.order.ShownName + (extras.Length > 0 ? "\n" + extras : "");
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
                if (p.order.unclear) TickPeek(p, dt);
                p.patience -= dt / PatienceSeconds(p.order);
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
        // pay depends on the patience bar: green = FAST bonus, red = late (part of the price, no tip).
        // Nobody can taste which grade went in.
        int price = p.order.Price;
        bool fast = p.patience >= balance.fastZone, late = p.patience <= balance.lateZone;
        int paid = late ? Mathf.RoundToInt(price * balance.latePay)
            : price + Mathf.RoundToInt(price * p.patience * balance.tipMultiplier) + (fast ? Mathf.RoundToInt(price * balance.fastBonus) : 0);
        // made an unclear order right without asking: the less the peek had shown, the bigger the bonus
        int bonus = p.order.unclear ? GuessBonusNow(p) : 0;
        bool guessed = bonus > 0;
        paid += bonus;
        Coins += paid;
        Today.happy++;
        Today.earned += paid;
        string cheer = guessed ? "GOOD GUESS!" : fast ? "SO FAST!" : late ? "FINALLY..." : "THANKS!";
        React(p, $"{cheer}\n<color=#2e5222>+{paid}</color>", "1e1a2b");
        SoundFX.Play(Sfx.ServeGood);
        return true;
    }

    void React(Patron p, string message, string color)
    {
        p.stage = Stage.Leaving;
        p.timer = reactTime + fadeTime;
        LastReaction = message;
        pickup[p.spot].ticketText.text = $"<color=#{color}>{message}</color>";
        if (selected == p.spot)
        {
            CloseGuess();
            SelectOldest();
        }
        RefreshAll();
    }

    // ---- selection ---------------------------------------------------------

    public void Select(int spot)
    {
        if (spot < 0 || spot >= waiting.Length || waiting[spot] == null || waiting[spot].stage != Stage.Waiting) return;
        if (spot != selected) CloseGuess();
        selected = spot;
        recipePanel.ShowTab(RecipePanel.Tab.Recipe);
        RefreshTickets();
        RefreshRecipe();
    }

    // Clicking a ticket selects it; clicking an unclear one also asks what they meant.
    public void ClickTicket(int spot)
    {
        Select(spot);
        if (selected == spot && Selected.order.unclear) OpenGuess();
    }

    // ---- guessing ----------------------------------------------------------

    // G or clicking an unclear ticket: show the 3 names over that customer.
    public void OpenGuess()
    {
        var p = Selected;
        if (p == null || p.stage != Stage.Waiting || !p.order.unclear || guessPopup == null) return;
        float center = pickup[p.spot].view.rectTransform.anchoredPosition.x;
        guessPopup.anchoredPosition = new Vector2(Mathf.Clamp(Mathf.Round(center - guessPopup.sizeDelta.x / 2), 4, 297 - guessPopup.sizeDelta.x), -guessTop);
        for (int i = 0; i < guessButtons.Length; i++)
        {
            bool wrong = p.wrongGuesses.Contains(i);
            string name = i < p.guesses.Count ? p.guesses[i].displayName.ToUpper() : "";
            guessLabels[i].text = wrong ? $"<color=#8a7a5a><s>{name}</s></color>" : name;
            guessButtons[i].interactable = !wrong && i < p.guesses.Count;
        }
        guessPopup.gameObject.SetActive(true);
    }

    public void CloseGuess()
    {
        if (guessPopup != null) guessPopup.gameObject.SetActive(false);
    }

    // Right: the order is revealed. Wrong: crossed out, and everyone waiting loses patience.
    public void Guess(int option)
    {
        var p = Selected;
        if (!Guessing || p == null || !p.order.unclear || option >= p.guesses.Count || p.wrongGuesses.Contains(option)) return;
        if (p.guesses[option] == p.order.recipe)
        {
            p.order.unclear = false;
            LastReaction = "THAT'S IT!";
            SoundFX.Play(Sfx.Order);
            CloseGuess();
            RefreshTicketText(p);
            RefreshAll();
            return;
        }
        p.wrongGuesses.Add(option);
        LastReaction = "NO...";
        SoundFX.Play(Sfx.ServeBad);
        float penalty = balance.wrongGuessPenalty;
        foreach (var w in WaitingPatrons) w.patience -= penalty;
        foreach (var l in line) l.patience -= penalty;
        Debug.Log($"[Customers] Wrong guess: everyone waiting lost {penalty:P0} patience");
        OpenGuess(); // redraw with the crossed-out name
        RefreshAll();
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
        // green while serving pays the FAST bonus, red once it'd be late
        s.patienceFill.GetComponent<Image>().color = amount >= balance.fastZone ? PatienceOk
            : amount <= balance.lateZone ? PatienceLow : PatienceMid;
        s.view.color = amount <= balance.lateZone && p.stage == Stage.Waiting ? Color.Lerp(Color.white, Impatient, 0.5f) : Color.white;
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
            for (int i = 0; i < servedDots.Length; i++)
                servedDots[i].SetActive(i < Today.happy * servedDots.Length / Mathf.Max(1, dailyGoal));
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
        string others = WaitingCount > 1 ? $" <color=#8a7a5a>{WaitingPatrons.Count(w => w.spot < p.spot) + 1}/{WaitingCount}</color>" : "";
        if (order.unclear)
        {
            int bonus = GuessBonusNow(p);
            const int charsPerRow = 38; // the mumble wraps over this many rows
            int mumbleRows = 2 + Mathf.CeilToInt(order.Mumble.Length / (float)charsPerRow) + (order.snack != null ? 1 : 0);
            recipePanel.SetOrder($"#{p.spot + 1} {order.clue}{others}",
                "THEY MUMBLE. WATCH, OR G TO ASK:\n" + Peek(p, false) +
                (order.snack != null ? "\n+ " + order.snack.displayName.ToUpper() : "") +
                (bonus > 0 ? $"\n<color=#2e5222>MAKE IT NOW: +{bonus} BONUS</color>" : "\n<color=#8a7a5a>NO BONUS LEFT</color>"), mumbleRows);
            return;
        }
        var result = order.Check(cup.Counts());
        var rows = new List<string>();
        for (int i = 0; i < order.lines.Count; i++)
        {
            var line = order.lines[i];
            string key = $"<color=#8a7a5a>{i + 1}</color>"; // SHIFT+number jumps to this line
            if (line.removed) { rows.Add($"{key}[-] <s>{line.Name}</s> NONE"); continue; }
            string count = result.have[i] > 0 && !result.lineOk[i] ? $" ({result.have[i]}/{line.portions})" : "";
            rows.Add($"{key}[{(result.lineOk[i] ? "x" : " ")}] {line.Name} {line.Amount}{count}");
        }
        if (order.snack != null) rows.Add($"<color=#8a7a5a>{order.lines.Count + 1}</color>" + SnackRow(order));
        if (result.extras.Count > 0)
            rows.Add("<color=#8a2e2c>[!] NOT ORDERED: " +
                     string.Join(", ", result.extras.Select(n => n.displayName.ToUpper())) + "</color>");
        recipePanel.SetOrder($"#{p.spot + 1} {order.recipe.displayName.ToUpper()}{others}", string.Join("\n", rows), rows.Count);
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
