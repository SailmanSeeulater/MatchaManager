using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Turns the Obsidian "Matcha Manager" vault into DrinkNode assets.
// Starting at "Drink Tree.md", every "- [[Link]]" list item is a child.
// A link to a note that exists is a folder; a link to a missing note is an ingredient.
// Re-importing updates names/children but keeps portion and color tweaks made in the Inspector,
// and deletes ingredients and folders the vault no longer has (unless a recipe still uses them).
// Also creates starter recipes in Assets/Data/Recipes (only ones that don't exist yet).
public static class DrinkTreeImporter
{
    const string VaultPrefKey = "Matcha.VaultPath";
    const string DefaultVault = "/Users/late_root/Documents/Cats_Basement/Matcha Manager";
    const string RootNote = "Drink Tree";
    public const string OutDir = "Assets/Data/DrinkTree";
    public static string RootAssetPath => $"{OutDir}/{RootNote}.asset";
    public const string RecipeDir = "Assets/Data/Recipes";

    static readonly Regex ListItem = new Regex(@"^\s*-\s");
    static readonly Regex WikiLink = new Regex(@"\[\[([^\]|#]+)");

    static readonly Dictionary<string, string> Colors = new Dictionary<string, string>
    {
        { "Whole Milk", "f7f4ec" }, { "Half and Half", "f3ead6" }, { "Heavy Cream", "fff8e7" },
        { "Oat Milk", "e8d9b5" }, { "Almond Milk", "efe0c8" }, { "Soy Milk", "f1e6c6" }, { "Pistachio Milk", "d8e3b0" },
        { "Cane Sugar", "d9b77a" }, { "White Sugar", "ffffff" }, { "Brown Sugar", "a0703c" },
        { "Stevia", "e6f2d5" },
        { "Maple Syrup", "c47f2c" }, { "Vanilla Syrup", "f2e2b0" }, { "Ube Syrup", "8a5fb0" },
        { "Ceremonial Matcha", "6fa04a" }, { "Low-Grade Matcha", "8a9a4a" },
        { "Hojicha", "8a5432" }, { "Jasmine", "e8dc9a" }, { "Earl Gray", "b07a3c" },
        { "Arabrica", "5e3720" }, { "Robusta", "4a2c1d" }, { "Decaf", "7a5236" }, { "Cold Brew", "3a2216" },
        { "Dark Chocolate", "3b2418" }, { "Milk Chocolate", "7a4a2e" }, { "White Chocolate", "f4ecd8" },
        { "Water", "c9ecf7" }, { "Hot Water", "d8f1f8" }, { "Cold Water", "b5e4f4" }, { "Sparkling Water", "dff6fb" },
        { "Ice", "e4f8fc" },
        { "Coconut Flakes", "fbf8f0" }, { "Caramel Honeycomb", "e0a040" }, { "Caramel Drizzle", "b8702a" },
        { "Biscoff Crumbles", "b5773a" },
        { "Cheese Foam", "fff3c8" }, { "Sesame Foam", "e3d3b0" }, { "Matcha Foam", "c5e09a" },
    };

    [MenuItem("Matcha/Import Drink Tree")]
    public static void Import()
    {
        var vault = EditorPrefs.GetString(VaultPrefKey, DefaultVault);
        if (!Directory.Exists(vault) && !Application.isBatchMode)
        {
            vault = EditorUtility.OpenFolderPanel("Pick the Matcha Manager Obsidian vault", "", "");
            if (string.IsNullOrEmpty(vault)) return;
            EditorPrefs.SetString(VaultPrefKey, vault);
        }
        if (!File.Exists(Path.Combine(vault, RootNote + ".md")))
        {
            Debug.LogError($"[DrinkTree] No '{RootNote}.md' in {vault}");
            return;
        }

        Directory.CreateDirectory(OutDir);
        var root = ImportNode(RootNote, vault, new List<string>(), new HashSet<string>());
        CreateDefaultRecipes();
        RemoveOrphans(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DrinkTree] Imported from {vault}:\n{Describe(root, 0)}");
        ReportCoverage(root);
    }

    static List<DrinkRecipe> AllRecipes()
    {
        var list = new List<DrinkRecipe>();
        if (!AssetDatabase.IsValidFolder(RecipeDir)) return list;
        foreach (var guid in AssetDatabase.FindAssets("t:DrinkRecipe", new[] { RecipeDir }))
            list.Add(AssetDatabase.LoadAssetAtPath<DrinkRecipe>(AssetDatabase.GUIDToAssetPath(guid)));
        return list;
    }

    // Nodes left over from an older vault layout (Flavors, Vanilla, Maple Sugar...).
    static void RemoveOrphans(DrinkNode root)
    {
        var inTree = new HashSet<DrinkNode>();
        void Walk(DrinkNode n) { if (inTree.Add(n)) foreach (var c in n.children) Walk(c); }
        Walk(root);
        var used = new HashSet<DrinkNode>();
        foreach (var r in AllRecipes()) foreach (var l in r.lines) if (l.node != null) used.Add(l.node);
        foreach (var guid in AssetDatabase.FindAssets("t:DrinkNode", new[] { OutDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var node = AssetDatabase.LoadAssetAtPath<DrinkNode>(path);
            if (node == null || inTree.Contains(node)) continue;
            if (used.Contains(node))
                Debug.LogWarning($"[DrinkTree] '{node.displayName}' is no longer in the vault but a recipe still uses it; kept {path}");
            else
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[DrinkTree] Removed {path} (no longer in the vault)");
            }
        }
    }

    // Every ingredient should be wanted by at least one recipe, or nobody ever asks for it.
    static void ReportCoverage(DrinkNode root)
    {
        var recipes = AllRecipes();
        var unused = new List<string>();
        foreach (var leaf in root.Ingredients())
            if (!recipes.Exists(r => r.lines.Exists(l => l.node != null && l.node.Ingredients().Contains(leaf))))
                unused.Add(leaf.displayName);
        if (unused.Count == 0) Debug.Log($"[DrinkTree] Every ingredient is used by one of the {recipes.Count} recipes");
        else Debug.LogWarning($"[DrinkTree] No recipe uses: {string.Join(", ", unused)}");
    }

    // Sunday Matcha is from the vault's game notes; the others are starters to edit or delete.
    // Between them every ingredient in the tree is wanted by some drink: named when there's only
    // one of it, or through an ANY line where the player picks the grade (milks, beans, chocolates).
    // Lines: node name, portions, label, optional ("no ..." allowed).
    const int DefaultsVersion = 4;

    static void CreateDefaultRecipes()
    {
        var milk = ("Dairy", 1, "Milk", true);
        var anyMilk = ("Dairy", 1, "Milk", false);
        var sweetener = ("Sweeteners", 1, "Sweetener", true);
        // matcha lines take either grade: Low-Grade is cheaper and the customer can't tell
        var matcha = ("Matcha", 1, "Matcha", false);
        var coffee = ("Coffee", 1, "Coffee", false);
        (string, int, string, bool) One(string node, bool optional = false) => (node, 1, null, optional);
        MigrateRecipes();

        Recipe("Sunday Matcha", 6, matcha, One("Hot Water"), milk, sweetener);
        Recipe("Hojicha Latte", 5, One("Hojicha"), One("Hot Water"), anyMilk, sweetener);
        Recipe("Iced Matcha", 5, matcha, One("Cold Water"), One("Ice"), sweetener);
        Recipe("Mocha", 6, coffee, ("Chocolate", 1, "Chocolate", false), anyMilk);
        Recipe("Matcha Latte", 5, matcha, One("Hot Water"), ("Dairy", 2, "Milk", false), sweetener);
        Recipe("London Fog", 6, One("Earl Gray"), One("Hot Water"), anyMilk, One("Vanilla Syrup", true));
        Recipe("Jasmine Milk Tea", 5, One("Jasmine"), One("Hot Water"), milk, sweetener);
        Recipe("Iced Coffee", 5, coffee, One("Ice"), One("Cold Water"), milk, sweetener);
        Recipe("Hot Chocolate", 5, ("Chocolate", 2, "Chocolate", false), anyMilk, sweetener);

        // coffee bar
        Recipe("Cold Brew", 5, One("Cold Brew"), One("Cold Water"), One("Ice"), sweetener);
        Recipe("Coconut Cold Brew", 6, One("Cold Brew"), One("Ice"), milk, One("Coconut Flakes"));
        Recipe("Caramel Macchiato", 7, coffee, One("Vanilla Syrup"), anyMilk, One("Caramel Drizzle"));
        Recipe("Honeycomb Latte", 7, coffee, anyMilk, One("Brown Sugar"), One("Caramel Honeycomb"));
        Recipe("Biscoff Latte", 7, coffee, anyMilk, sweetener, One("Biscoff Crumbles"));
        Recipe("White Mocha", 6, coffee, One("White Chocolate"), anyMilk);
        Recipe("Skinny Latte", 5, One("Decaf"), One("Almond Milk"), One("Stevia"));
        Recipe("Breve", 5, coffee, One("Half and Half"), One("Cane Sugar", true));
        Recipe("Vienna Coffee", 6, coffee, One("Hot Water"), One("Heavy Cream"), One("White Sugar", true));
        // tea bar
        Recipe("Matcha Fizz", 6, matcha, One("Sparkling Water"), One("Ice"), ("Syrup", 1, "Syrup", true));
        Recipe("Pistachio Matcha", 6, matcha, One("Hot Water"), One("Pistachio Milk"), sweetener);
        Recipe("Matcha Cloud", 7, anyMilk, One("Ice"), One("Maple Syrup"), One("Matcha Foam"));
        Recipe("Cheese Foam Tea", 7, One("Jasmine"), One("Cold Water"), One("Ice"), One("Cheese Foam"));
        Recipe("Sesame Hojicha", 7, One("Hojicha"), One("Hot Water"), anyMilk, One("Sesame Foam"));
        Recipe("Ube Latte", 6, One("Ube Syrup"), One("Oat Milk"), One("Ice"));
    }

    // Updates the starter recipes made by older versions, once each. Build Scene calls it too.
    //   1: Ceremonial Matcha exactly -> any matcha (cost to operate: the grade is the player's choice)
    //   2: the drink tree regrouped: Vanilla -> Vanilla Syrup, tea brewed with Hot Water, iced
    //      drinks with Cold Water
    //   3: every hot drink made with water uses Hot Water (Sunday Matcha), and hot matcha
    //      drinks whisk their matcha in some (Matcha Latte, Pistachio Matcha)
    //   4: Matcha Latte takes a double milk, so it isn't the same drink as Sunday Matcha
    public static void MigrateRecipes()
    {
        DrinkNode Node(string name) => AssetDatabase.LoadAssetAtPath<DrinkNode>($"{OutDir}/{name}.asset");
        var matchaFolder = Node("Matcha");
        if (matchaFolder == null || !AssetDatabase.IsValidFolder(RecipeDir)) return;
        var vanillaSyrup = Node("Vanilla Syrup");
        var hotWater = Node("Hot Water");
        var coldWater = Node("Cold Water");
        bool regrouped = vanillaSyrup != null && hotWater != null && coldWater != null; // vault imported
        var hot = new[] { "Hojicha Latte", "London Fog", "Jasmine Milk Tea" };
        var cold = new[] { "Iced Matcha", "Iced Coffee" };
        var whisked = new[] { "Matcha Latte", "Pistachio Matcha" };
        foreach (var recipe in AllRecipes())
        {
            if (recipe.defaultsVersion < 1)
            {
                foreach (var line in recipe.lines)
                    if (line.node != null && line.node.displayName == "Ceremonial Matcha")
                    {
                        line.node = matchaFolder;
                        line.label = "Matcha";
                    }
                recipe.defaultsVersion = 1;
                EditorUtility.SetDirty(recipe);
            }
            if (recipe.defaultsVersion < 2 && regrouped)
            {
                foreach (var line in recipe.lines)
                {
                    if (line.node == null) continue;
                    var name = line.node.displayName;
                    if (name == "Vanilla" || name == "Real Vanilla" || name == "Synthetic Vanilla")
                    {
                        line.node = vanillaSyrup;
                        line.label = null;
                    }
                    else if (name == "Water" && hot.Contains(recipe.name)) line.node = hotWater;
                    else if (name == "Water" && cold.Contains(recipe.name)) line.node = coldWater;
                }
                recipe.defaultsVersion = 2;
                EditorUtility.SetDirty(recipe);
            }
            if (recipe.defaultsVersion < 3 && regrouped)
            {
                foreach (var line in recipe.lines)
                    if (line.node != null && line.node.displayName == "Water" && recipe.name == "Sunday Matcha") line.node = hotWater;
                if (whisked.Contains(recipe.name) && !recipe.lines.Exists(l => l.node == hotWater))
                {
                    int matchaLine = recipe.lines.FindIndex(l => l.node == matchaFolder);
                    recipe.lines.Insert(matchaLine + 1, new DrinkRecipe.Line { node = hotWater, portions = 1 });
                }
                recipe.defaultsVersion = 3;
                EditorUtility.SetDirty(recipe);
            }
            if (recipe.defaultsVersion < 4 && regrouped)
            {
                if (recipe.name == "Matcha Latte")
                    foreach (var line in recipe.lines)
                        if (line.node != null && line.node.displayName == "Dairy" && line.portions == 1) line.portions = 2;
                recipe.defaultsVersion = 4;
                EditorUtility.SetDirty(recipe);
            }
        }
        AssetDatabase.SaveAssets();
    }

    static void Recipe(string name, int price, params (string node, int portions, string label, bool optional)[] lines)
    {
        var path = $"{RecipeDir}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<DrinkRecipe>(path) != null) return;
        var recipe = ScriptableObject.CreateInstance<DrinkRecipe>();
        recipe.displayName = name;
        recipe.price = price;
        recipe.defaultsVersion = DefaultsVersion;
        foreach (var l in lines)
        {
            var node = AssetDatabase.LoadAssetAtPath<DrinkNode>($"{OutDir}/{l.node}.asset");
            if (node == null)
            {
                Debug.LogWarning($"[DrinkTree] Skipped recipe '{name}': no '{l.node}' in the drink tree");
                return;
            }
            recipe.lines.Add(new DrinkRecipe.Line { node = node, portions = l.portions, label = l.label, optional = l.optional });
        }
        Directory.CreateDirectory(RecipeDir);
        AssetDatabase.CreateAsset(recipe, path);
        Debug.Log($"[DrinkTree] Created recipe {path}");
    }

    static DrinkNode ImportNode(string name, string vault, List<string> ancestors, HashSet<string> visiting)
    {
        var assetPath = $"{OutDir}/{name}.asset";
        var node = AssetDatabase.LoadAssetAtPath<DrinkNode>(assetPath);
        bool created = node == null;
        if (created)
        {
            node = ScriptableObject.CreateInstance<DrinkNode>();
            AssetDatabase.CreateAsset(node, assetPath);
        }
        node.displayName = name;
        node.children.Clear();

        var notePath = Path.Combine(vault, name + ".md");
        if (File.Exists(notePath) && visiting.Add(name))
        {
            ancestors.Add(name);
            foreach (var child in ParseChildren(File.ReadAllText(notePath)))
                node.children.Add(ImportNode(child, vault, ancestors, visiting));
            ancestors.RemoveAt(ancestors.Count - 1);
            visiting.Remove(name);
        }

        if (created && node.IsIngredient) ApplyDefaults(node, ancestors);
        EditorUtility.SetDirty(node);
        return node;
    }

    static List<string> ParseChildren(string markdown)
    {
        var children = new List<string>();
        foreach (var line in markdown.Split('\n'))
        {
            if (!ListItem.IsMatch(line)) continue;
            foreach (Match m in WikiLink.Matches(line))
            {
                var link = m.Groups[1].Value.Trim();
                if (link.Length > 0 && !children.Contains(link)) children.Add(link);
            }
        }
        return children;
    }

    // Portions come from the Sunday Matcha recipe where it gives one; the rest are guesses to tune.
    static void ApplyDefaults(DrinkNode n, List<string> ancestors)
    {
        bool Under(string folder) => ancestors.Contains(folder);
        (float portion, string unit, int px) d =
            n.displayName.EndsWith("Water") ? (4, "oz", 8) :
            n.displayName == "Ice" ? (1, "scoop", 4) :
            n.displayName == "Cold Brew" ? (4, "oz", 8) :
            n.displayName == "Caramel Drizzle" ? (1, "drizzle", 2) :
            Under("Dairy") ? (6, "oz", 12) :
            Under("Syrup") ? (1, "pump", 4) :
            Under("Sweeteners") ? (15, "g", 2) :
            Under("Matcha") ? (100, "g", 8) :
            Under("Tea") ? (1, "bag", 8) :
            Under("Coffee") ? (1, "shot", 8) :
            Under("Chocolate") ? (1, "pump", 4) :
            Under("Foam") ? (2, "oz", 4) :
            Under("Garnish") ? (1, "pinch", 2) :
            (1, "", 4);
        n.portion = d.portion;
        n.unit = d.unit;
        n.cupPixels = d.px;
        n.cupColor = Colors.TryGetValue(n.displayName, out var hex) && ColorUtility.TryParseHtmlString("#" + hex, out var c)
            ? c : Color.white;
    }

    static string Describe(DrinkNode n, int depth)
    {
        var s = new string(' ', depth * 2) + n.displayName + (n.IsIngredient ? $" (+{n.PortionLabel})" : "") + "\n";
        foreach (var c in n.children) s += Describe(c, depth + 1);
        return s;
    }
}
