using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// What one customer asked for: a recipe plus their changes to it, and maybe a snack,
// e.g. "Sunday Matcha, no milk, extra cane sugar, also I need a danish".
public class Order
{
    public class Line
    {
        public DrinkNode node;   // ingredient, or folder = any ingredient inside it
        public int portions;
        public string label;
        public bool removed;     // "no milk": nothing from this line may be in the cup

        public bool IsAny => !node.IsIngredient;
        public string Name => (IsAny ? "ANY " : "") + (string.IsNullOrEmpty(label) ? node.displayName : label).ToUpper();

        // e.g. "12 OZ" for 2 portions of a 6 oz milk; folders use their first ingredient's portion
        public string Amount
        {
            get
            {
                var leaf = node.Ingredients().First();
                return $"{leaf.portion * portions:0.##} {leaf.unit}".Trim().ToUpper();
            }
        }
    }

    public class Result
    {
        public int[] have;                // portions in the cup for each line
        public bool[] lineOk;
        public List<DrinkNode> extras;    // in the cup but not in the order
        public bool Correct => lineOk.All(ok => ok) && extras.Count == 0;
    }

    public readonly DrinkRecipe recipe;
    // Unclear: the customer only said part of the name (clue, e.g. "??? MATCHA"). Until the player
    // asks and guesses right, the name and the changes stay hidden.
    public bool unclear;
    public string clue;
    public string ShownName => unclear ? clue : recipe.displayName.ToUpper();
    public readonly Snack snack; // null = drink only
    public readonly List<Line> lines;
    public readonly List<string> requests = new List<string>(); // shown in the bubble

    public Order(DrinkRecipe recipe, Snack snack = null)
    {
        this.recipe = recipe;
        this.snack = snack;
        lines = recipe.lines.Where(l => l.node != null).Select(l => new Line
        {
            node = l.node, portions = Mathf.Max(1, l.portions), label = l.label,
        }).ToList();
    }

    // Coins for getting it right, before the tip.
    public int Price => recipe.price + (snack != null ? snack.price : 0);

    // Recipe plus random changes: NO (an optional line), EXTRA (one more portion, sometimes two),
    // WITH (a specific choice for an "any" line) or ADD (a small extra the recipe doesn't have).
    // Each change is rolled after the last one succeeds, so long lists are rarer; how often and how many
    // come from Balance and grow with the day. Defaults are used without a Balance.
    public static Order Random(DrinkRecipe recipe, IList<Snack> snacks, Balance balance = null, int day = 1,
        IList<DrinkNode> addable = null)
    {
        float snackChance = balance != null ? balance.snackChance : 0.4f;
        float changeChance = balance != null ? balance.ChangeChance(day) : 0.5f;
        int maxChanges = balance != null ? balance.maxChanges : 2;
        float addChance = balance != null ? balance.addChance : 0f;
        float doubleChance = balance != null ? balance.doubleExtraChance : 0f;

        var snack = snacks.Count > 0 && UnityEngine.Random.value < snackChance
            ? snacks[UnityEngine.Random.Range(0, snacks.Count)] : null;
        var order = new Order(recipe, snack);
        var untouched = Enumerable.Range(0, order.lines.Count).ToList();
        for (int c = 0; c < maxChanges && UnityEngine.Random.value < changeChance; c++)
        {
            if (addable != null && UnityEngine.Random.value < addChance && order.TryAdd(addable)) continue;
            if (untouched.Count == 0) break;
            int pick = UnityEngine.Random.Range(0, untouched.Count);
            int i = untouched[pick];
            untouched.RemoveAt(pick);
            var line = order.lines[i];

            var kinds = new List<int> { 1 };
            if (recipe.lines[i].optional) kinds.Add(0);
            if (line.IsAny) kinds.Add(2);
            switch (kinds[UnityEngine.Random.Range(0, kinds.Count)])
            {
                case 0:
                    order.requests.Add("NO " + (string.IsNullOrEmpty(line.label) ? line.node.displayName : line.label).ToUpper());
                    line.removed = true;
                    line.portions = 0;
                    break;
                case 1:
                    order.Specify(line);
                    int more = UnityEngine.Random.value < doubleChance ? 2 : 1;
                    line.portions += more;
                    order.requests.Add("EXTRA " + line.Name + (more > 1 ? " x2" : ""));
                    break;
                case 2:
                    order.Specify(line, true);
                    order.requests.Add("WITH " + line.Name);
                    break;
            }
        }
        return order;
    }

    // ADD: one portion of something small the order doesn't already cover
    bool TryAdd(IList<DrinkNode> addable)
    {
        var options = addable.Where(n => !lines.Any(l => l.node.Ingredients().Contains(n))).ToList();
        if (options.Count == 0) return false;
        var pick = options[UnityEngine.Random.Range(0, options.Count)];
        lines.Add(new Line { node = pick, portions = 1 });
        requests.Add("ADD " + pick.displayName.ToUpper());
        return true;
    }

    // "any milk" -> a random milk (not one another line already asks for, e.g. an ADD)
    // WITH never names the cheapest choice, so pouring it from habit is the mistake it catches.
    void Specify(Line line, bool notCheapest = false)
    {
        if (!line.IsAny) return;
        var taken = lines.Where(l => l != line && !l.IsAny).Select(l => l.node).ToList();
        var options = line.node.Ingredients().Where(n => !taken.Contains(n)).ToList();
        if (options.Count == 0) options = line.node.Ingredients().ToList();
        if (notCheapest && options.Count > 1)
        {
            int cheapest = line.node.Ingredients().Min(n => n.cost);
            var pricier = options.Where(n => n.cost > cheapest).ToList();
            if (pricier.Count > 0) options = pricier;
        }
        line.node = options[UnityEngine.Random.Range(0, options.Count)];
        line.label = null;
    }

    public Result Check(Dictionary<DrinkNode, int> counts)
    {
        var remaining = new Dictionary<DrinkNode, int>(counts);
        var result = new Result { have = new int[lines.Count], lineOk = new bool[lines.Count] };
        // specific ingredients claim their portions before "any" lines do
        foreach (int i in Enumerable.Range(0, lines.Count).OrderBy(i => lines[i].IsAny))
        {
            var line = lines[i];
            if (line.removed) { result.lineOk[i] = true; continue; }
            foreach (var leaf in line.node.Ingredients())
            {
                if (!remaining.TryGetValue(leaf, out var n)) continue;
                result.have[i] += n;
                remaining.Remove(leaf);
            }
            result.lineOk[i] = result.have[i] == line.portions;
        }
        result.extras = remaining.Keys.ToList();
        return result;
    }

    // What the customer says about the snack they got, or null if it's right (or they didn't want one).
    public string SnackProblem(Snack got, Heat heat)
    {
        if (snack == null) return null;
        var name = snack.displayName.ToUpper();
        if (got == null) return $"WHERE'S MY\n{name}?";
        if (got != snack) return $"I WANTED\nA {name}";
        if (snack.needsHeating && heat == Heat.Underheated) return "MY " + name + "\nIS COLD!";
        if (snack.needsHeating && heat == Heat.Burnt) return "IT'S\nBURNT!";
        return null;
    }
}
