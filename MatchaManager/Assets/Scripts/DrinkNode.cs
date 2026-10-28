using System.Collections.Generic;
using UnityEngine;

// One entry in the drink tree. Nodes with children are folders (Dairy, Tea...),
// nodes without children are ingredients the player can add to the cup.
// Generated from the Obsidian vault by Matcha > Import Drink Tree.
[CreateAssetMenu(menuName = "Matcha/Drink Node")]
public class DrinkNode : ScriptableObject
{
    public string displayName;
    public List<DrinkNode> children = new List<DrinkNode>();

    [Header("Ingredient (nodes without children)")]
    [Tooltip("Recipe-sized amount one click adds, e.g. 6")]
    public float portion = 1;
    [Tooltip("Unit shown with the portion, e.g. oz, g, pump")]
    public string unit = "";
    [Tooltip("Color of this ingredient's layer in the cup")]
    public Color cupColor = Color.white;
    [Tooltip("How many pixels tall one portion fills the cup")]
    public int cupPixels = 6;

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
