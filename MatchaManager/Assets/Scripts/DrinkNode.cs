using System.Collections.Generic;
using UnityEngine;

// How an ingredient looks in the glass. Auto lets Build Scene pick from the drink tree
// (sweeteners and matcha are powders, Ice is ice, everything else is liquid).
public enum CupLook { Auto, Liquid, Powder, Ice }

// One entry in the drink tree. Nodes with children are folders (Dairy, Tea...),
// nodes without children are ingredients the player can add to the cup.
// Generated from the Obsidian vault by Matcha > Import Drink Tree.
[CreateAssetMenu(menuName = "Matcha/Drink Node")]
public class DrinkNode : ScriptableObject
{
    public string displayName;
    public List<DrinkNode> children = new List<DrinkNode>();
    [Tooltip("8x8 icon on its button; Build Scene fills it from Assets/Art/Icons/<name>.png, or the parent's icon")]
    public Sprite icon;

    [Header("Ingredient (nodes without children)")]
    [Tooltip("Recipe-sized amount one click adds, e.g. 6")]
    public float portion = 1;
    [Tooltip("Unit shown with the portion, e.g. oz, g, pump")]
    public string unit = "";
    [Tooltip("Color of this ingredient's layer in the cup")]
    public Color cupColor = Color.white;
    [Tooltip("How many pixels tall one portion fills the cup")]
    public int cupPixels = 6;
    [Tooltip("Liquid = smooth layer, Powder = grainy layer, Ice = cubes floating in the drink")]
    public CupLook look = CupLook.Auto;
    [Tooltip("Coins one portion costs the shop (times the difficulty's Cost Multiplier)")]
    public int cost = 1;
    [HideInInspector] public int economyVersion; // 0 = Build Scene fills in the default cost

    public bool IsIngredient => children.Count == 0;
    public string PortionLabel => $"{portion:0.##} {unit}".Trim();

    // Every ingredient at or below this node (an ingredient yields itself).
    public IEnumerable<DrinkNode> Ingredients()
    {
        if (IsIngredient) { yield return this; yield break; }
        foreach (var child in children)
            foreach (var leaf in child.Ingredients()) yield return leaf;
    }
}
