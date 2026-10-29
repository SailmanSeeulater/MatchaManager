using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Matcha > Simulate Balance: bot players of three skill levels play 10 days on
// each difficulty with the real game code and the Balance assets (Assets/Data/
// Balance Easy / Balance / Balance Hard), and the Console gets a comparison table.
// Use it after changing a Balance asset.
//
// A bot takes the oldest ticket it can still finish before that customer gives up
// (or the most patient one if none), reads it (think time), then clicks through the drink tree
// for each ingredient (folders in, portions, back out) at its click speed, with
// a chance of a mistake that costs a partial redo. It starts a hot snack heating
// first (microwave if the drink takes long enough, else the oven), waits for it
// if needed, and serves. Customers who leave mid-drink waste the bot's work.
public static class BalanceSim
{
    // How bots pick between cheap and premium ingredients for "any" lines.
    public enum Shopping { Smart, Cheapest, Premium }

    class Profile
    {
        public string name;
        public float click, think, mistake;
        public Profile(string name, float click, float think, float mistake)
        {
            this.name = name; this.click = click; this.think = think; this.mistake = mistake;
        }
    }

    static readonly Profile[] Profiles =
    {
        new Profile("BEGINNER", 0.9f, 4f, 0.15f),
        new Profile("REGULAR", 0.6f, 2f, 0.07f),
        new Profile("EXPERT", 0.4f, 1f, 0.02f),
    };
    const int Days = 10, Seeds = 3;
    const float Dt = 0.1f;
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Matcha/Simulate Balance")]
    public static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
        EditorSceneManager.OpenScene(ScenePath); // drop the simulated state
    }

    // Every difficulty: per bot, happy / lost (walked out + turned away) / runs reaching the goal, at days 1, 4, 7, 10,
    // plus how many changes orders have on day 1 and day 10.
    public static void Run()
    {
        var report = new StringBuilder($"[Balance] {Days} days x {Seeds} runs per bot. Cells: happy/lost/runs reaching the goal\n");
        var difficulties = MatchaSceneBuilder.EnsureDifficulties();
        for (int difficulty = 0; difficulty < difficulties.Length; difficulty++)
        {
            var balance = difficulties[difficulty];
            report.AppendLine($"{balance.difficultyName}: arrivals {balance.arrivalInterval.x:0}-{balance.arrivalInterval.y:0}s, patience x{balance.patienceMultiplier:0.##}, " +
                              $"goal {balance.dailyGoal}+{balance.goalPerDay}/day, {balance.harderPerDay:P0} harder/day to {balance.hardestAt:P0}, " +
                              $"changes per order: day 1 {AverageChanges(balance, 1):0.0}, day 10 {AverageChanges(balance, 10):0.0}");
            report.AppendLine($"  {"",-9} {"day 1",-14} {"day 4",-14} {"day 7",-14} {"day 10",-14}  (goal {balance.Goal(1)}/{balance.Goal(4)}/{balance.Goal(7)}/{balance.Goal(10)})");
            foreach (var profile in Profiles)
            {
                var days = Enumerable.Range(0, Days).Select(_ => new List<CustomerManager.DayStats>()).ToArray();
                for (int seed = 1; seed <= Seeds; seed++) SimulateWeek(profile, seed, days, difficulty);
                report.Append($"  {profile.name,-9}");
                foreach (int d in new[] { 0, 3, 6, 9 })
                    report.Append($" {days[d].Average(x => x.happy),4:0.0}/{days[d].Average(x => x.walkedOut + x.turnedAway),4:0.0}/{days[d].Count(x => x.happy >= balance.Goal(d + 1))}   ");
                report.AppendLine();
            }
        }
        // does matching palates pay? same regular bot, three ways of choosing ingredients (Medium)
        report.AppendLine("PROFIT by ingredient choice (REGULAR bot, MEDIUM): profit/happy at days 1, 5, 10");
        foreach (Shopping shopping in System.Enum.GetValues(typeof(Shopping)))
        {
            var days = Enumerable.Range(0, Days).Select(_ => new List<CustomerManager.DayStats>()).ToArray();
            for (int seed = 1; seed <= Seeds; seed++) SimulateWeek(Profiles[1], seed, days, 1, shopping);
            report.Append($"  {shopping,-9}");
            foreach (int d in new[] { 0, 4, 9 })
                report.Append($" {days[d].Average(x => x.earned - x.spent),5:0}/{days[d].Average(x => x.happy),4:0.0}  ");
            report.AppendLine();
        }
        Debug.Log(report.ToString());
    }

    static float AverageChanges(Balance balance, int day)
    {
        var recipes = AssetDatabase.FindAssets("t:DrinkRecipe", new[] { DrinkTreeImporter.RecipeDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<DrinkRecipe>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        var root = AssetDatabase.LoadAssetAtPath<DrinkNode>(DrinkTreeImporter.RootAssetPath);
        var addable = root.Ingredients().Where(n => n.cupPixels <= balance.addMaxPixels).ToList();
        Random.InitState(1);
        return (float)Enumerable.Range(0, 400)
            .Average(i => Order.Random(recipes[i % recipes.Count], new Snack[0], balance, day, addable).requests.Count);
    }

    static void SimulateWeek(Profile profile, int seed, List<CustomerManager.DayStats>[] days, int difficulty,
        Shopping shopping = Shopping.Smart)
    {
        EditorSceneManager.OpenScene(ScenePath);
        Object.FindAnyObjectByType<TitleScreen>().gameObject.SetActive(false);
        var day = Object.FindAnyObjectByType<DayCycle>();
        var customers = day.customers;
        var browser = Object.FindAnyObjectByType<DrinkBrowser>();
        var snacks = day.snacks;
        SaveSystem.PathOverride = "Temp/BalanceSimSave.json";
        SaveSystem.Delete();
        Random.InitState(seed * 7919);
        customers.Init();
        browser.ShowRoot();
        day.StartNewGame(difficulty);

        var depth = new Dictionary<DrinkNode, int>();
        void Walk(DrinkNode n, int d) { depth[n] = d; foreach (var c in n.children) Walk(c, d + 1); }
        Walk(browser.root, 0);

        for (int d = 0; d < Days; d++)
        {
            var bot = new Bot(profile, customers, snacks, depth) { shopping = shopping };
            float t = 0;
            while (!day.Ended && t < 1200)
            {
                day.Tick(Dt);
                customers.Tick(Dt);
                snacks.Tick(Dt);
                bot.Step(t);
                t += Dt;
            }
            days[d].Add(customers.Today);
            day.NextDay();
        }
        SaveSystem.Delete();
        SaveSystem.PathOverride = null;
    }

    class Bot
    {
        readonly Profile profile;
        public Shopping shopping;
        readonly CustomerManager customers;
        readonly SnackStation snacks;
        readonly Cup cup;
        readonly Dictionary<DrinkNode, int> depth;
        CustomerManager.Patron target;
        float doneAt;
        int heating = -1; // appliance in use for the target's snack

        public Bot(Profile profile, CustomerManager customers, SnackStation snacks, Dictionary<DrinkNode, int> depth)
        {
            this.profile = profile; this.customers = customers; this.snacks = snacks; this.depth = depth;
            cup = customers.cup;
        }

        public void Step(float t)
        {
            if (target != null && target.stage != CustomerManager.Stage.Waiting)
            {
                // they left before the drink was ready: dump it (costs everyone patience, like for a player)
                cup.PourOut();
                if (heating >= 0 && snacks.appliances[heating].snack != null) snacks.ClickAppliance(heating);
                snacks.ClearTray();
                heating = -1;
                target = null;
            }
            if (target == null) Start(t);
            else if (t >= doneAt) Finish();
        }

        void Start(float t)
        {
            // oldest ticket that can still be made in time, else whoever will wait longest
            var waiting = customers.WaitingPatrons.ToList();
            if (waiting.Count == 0) return;
            float SecondsLeft(CustomerManager.Patron p) => p.patience * customers.PatienceSeconds(p.order);
            target = waiting.Where(p => SecondsLeft(p) > Estimate(p.order) + 1).OrderBy(p => p.ticketTime).FirstOrDefault()
                     ?? waiting.OrderByDescending(SecondsLeft).First();
            var order = target.order;
            float asking = 0;
            if (order.unclear)
            {
                // ask: try the names in random order until the right one (wrong ones cost everyone patience)
                customers.Select(target.spot);
                customers.OpenGuess();
                foreach (int i in Enumerable.Range(0, target.guesses.Count).OrderBy(_ => Random.value))
                {
                    asking += profile.click;
                    customers.Guess(i);
                    if (!order.unclear) break;
                }
                customers.CloseGuess();
            }
            float clicks = Clicks(order);
            float time = profile.think + clicks * profile.click + asking;
            if (Random.value < profile.mistake)
            {
                // a mistake means dumping and redoing part of the drink
                time += clicks * profile.click * 0.6f + profile.click;
                customers.DumpPenalty();
            }
            if (order.snack != null)
            {
                if (order.snack.needsHeating)
                {
                    var micro = snacks.appliances[0];
                    heating = time >= micro.readySeconds && time + profile.click < micro.burnSeconds ? 0 : 1;
                    if (snacks.appliances[heating].snack != null) heating = 1 - heating;
                    snacks.ClickSlot(System.Array.FindIndex(snacks.slots, s => s.snack == order.snack));
                    snacks.ClickAppliance(heating);
                    time += 3 * profile.click;
                }
                else time += profile.click;
            }
            doneAt = t + time;
        }

        // folders in, one click per portion, back out; then serve
        float Clicks(Order order) => 1 + order.lines.Where(l => !l.removed)
            .Sum(l => 2 * depth[l.node.Ingredients().First()] - 2 + l.portions);

        float Estimate(Order order) =>
            profile.think + Clicks(order) * profile.click +
            (order.snack == null ? 0 : order.snack.needsHeating ? Mathf.Max(3 * profile.click, snacks.appliances[0].readySeconds) : profile.click);

        // Smart: the cheapest ingredient good enough for this customer; Cheapest / Premium ignore the palate.
        DrinkNode Choose(DrinkNode node, int palate)
        {
            var options = node.Ingredients().ToList();
            switch (shopping)
            {
                case Shopping.Cheapest: return options.OrderBy(n => n.cost).ThenByDescending(n => n.quality).First();
                case Shopping.Premium: return options.OrderByDescending(n => n.quality).ThenBy(n => n.cost).First();
                default:
                    return options.Where(n => n.quality == 0 || n.quality >= palate).OrderBy(n => n.cost).FirstOrDefault()
                           ?? options.OrderByDescending(n => n.quality).First();
            }
        }

        void Finish()
        {
            var order = target.order;
            if (heating >= 0)
            {
                if (snacks.appliances[heating].Heat == Heat.Underheated) return; // wait for it
                snacks.ClickAppliance(heating);
                heating = -1;
            }
            else if (order.snack != null)
                snacks.ClickSlot(System.Array.FindIndex(snacks.slots, s => s.snack == order.snack));
            foreach (var line in order.lines.Where(l => !l.removed))
            {
                var pick = Choose(line.node, target.palate);
                for (int i = 0; i < line.portions; i++) cup.Add(pick);
            }
            cup.Serve();
            if (!cup.IsEmpty) cup.Clear();
            target = null;
        }
    }
}
