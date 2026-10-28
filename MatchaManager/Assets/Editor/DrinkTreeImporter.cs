using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Turns the Obsidian "Matcha Manager" vault into DrinkNode assets.
// Starting at "Drink Tree.md", every "- [[Link]]" list item is a child.
// A link to a note that exists is a folder; a link to a missing note is an ingredient.
// Re-importing updates names/children but keeps portion and color tweaks made in the Inspector.
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
        { "Maple Sugar", "c47f2c" }, { "Stevia", "e6f2d5" },
        { "Ceremonial Matcha", "6fa04a" }, { "Low-Grade Matcha", "8a9a4a" },
        { "Hojicha", "8a5432" }, { "Jasmine", "e8dc9a" }, { "Earl Gray", "b07a3c" },
        { "Arabrica", "5e3720" }, { "Robusta", "4a2c1d" }, { "Decaf", "7a5236" },
        { "Dark Chocolate", "3b2418" }, { "Milk Chocolate", "7a4a2e" }, { "White Chocolate", "f4ecd8" },
        { "Synthetic Vanilla", "f2e6c0" }, { "Real Vanilla", "eadbb0" },
        { "Water", "c9ecf7" }, { "Ice", "e4f8fc" },
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
        AssetDatabase.SaveAssets();
        Debug.Log($"[DrinkTree] Imported from {vault}:\n{Describe(root, 0)}");
    }

    // Sunday Matcha is from the vault's game notes; the others are starters to edit or delete.
    // Lines: node name, portions, label, optional ("no ..." allowed).
    static void CreateDefaultRecipes()
    {
        var milk = ("Dairy", 1, "Milk", true);
        var sweetener = ("Sweeteners", 1, "Sweetener", true);
        Recipe("Sunday Matcha", 6, 45, ("Ceremonial Matcha", 1, null, false), ("Water", 1, null, false), milk, sweetener);
        Recipe("Hojicha Latte", 5, 45, ("Hojicha", 1, null, false), ("Water", 1, null, false), ("Dairy", 1, "Milk", false), sweetener);
        Recipe("Iced Matcha", 5, 40, ("Ceremonial Matcha", 1, null, false), ("Water", 1, null, false), ("Ice", 1, null, false), sweetener);
        Recipe("Mocha", 6, 50, ("Coffee", 1, "Coffee", false), ("Chocolate", 1, "Chocolate", false), ("Dairy", 1, "Milk", false));
    }

    static void Recipe(string name, int price, float patience, params (string node, int portions, string label, bool optional)[] lines)
    {
        var path = $"{RecipeDir}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<DrinkRecipe>(path) != null) return;
        var recipe = ScriptableObject.CreateInstance<DrinkRecipe>();
        recipe.displayName = name;
        recipe.price = price;
        recipe.patienceSeconds = patience;
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
            n.displayName == "Water" ? (4, "oz", 8) :
            n.displayName == "Ice" ? (1, "scoop", 4) :
            Under("Dairy") ? (6, "oz", 12) :
            Under("Sweeteners") ? (15, "g", 2) :
            Under("Matcha") ? (100, "g", 8) :
            Under("Tea") ? (1, "bag", 8) :
            Under("Coffee") ? (1, "shot", 8) :
            Under("Chocolate") || Under("Vanilla") ? (1, "pump", 4) :
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
