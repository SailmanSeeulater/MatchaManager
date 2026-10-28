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

    public const float SnackChance = 0.4f;

    public readonly DrinkRecipe recipe;
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
    // Hot snacks take a while, so customers wait longer for them.
    public float PatienceSeconds => recipe.patienceSeconds + (snack != null && snack.needsHeating ? 10 : 0);

    // Recipe with 0-2 random changes: leave out an optional line, extra of something, or a specific choice for an "any" line.
    public static Order Random(DrinkRecipe recipe, IList<Snack> snacks)
    {
        var snack = snacks.Count > 0 && UnityEngine.Random.value < SnackChance
            ? snacks[UnityEngine.Random.Range(0, snacks.Count)] : null;
        var order = new Order(recipe, snack);
        int changes = UnityEngine.Random.value < 0.4f ? 0 : UnityEngine.Random.value < 0.7f ? 1 : 2;
        var untouched = Enumerable.Range(0, order.lines.Count).ToList();
        for (int c = 0; c < changes && untouched.Count > 0; c++)
        {
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
                    line.portions++;
                    order.requests.Add("EXTRA " + line.Name);
                    break;
                case 2:
                    order.Specify(line);
                    order.requests.Add("WITH " + line.Name);
                    break;
            }
        }
        return order;
    }

    // "any milk" -> a random milk
    void Specify(Line line)
    {
        if (!line.IsAny) return;
        var options = line.node.Ingredients().ToList();
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
