using System;
using System.Collections.Generic;
using UnityEngine;

// A drink on the menu. Each line is an ingredient, or a folder of the drink tree
// meaning "any ingredient inside it" (Dairy = any milk).
// Defaults are created by Matcha > Import Drink Tree; edit them in the Inspector.
[CreateAssetMenu(menuName = "Matcha/Drink Recipe")]
public class DrinkRecipe : ScriptableObject
{
    [Serializable]
    public class Line
    {
        [Tooltip("An ingredient, or a folder meaning any ingredient inside it")]
        public DrinkNode node;
        [Tooltip("How many clicks (portions) of it")]
        public int portions = 1;
        [Tooltip("Name shown instead of the node's, e.g. MILK for Dairy")]
        public string label;
        [Tooltip("Customers may ask to leave this out (\"no milk\")")]
        public bool optional;
    }

    public string displayName;
    public List<Line> lines = new List<Line>();
    [Tooltip("Coins for a correct drink; a happy customer tips up to the same again")]
    public int price = 5;
    [HideInInspector] public int defaultsVersion; // lets Import Drink Tree update starter recipes it made
}
