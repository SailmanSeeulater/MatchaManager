using UnityEngine;

// Something from the display cases. Cold snacks go straight onto the tray;
// hot ones have to be heated in the microwave or toaster oven first.
// Defaults are created by Matcha > Build Scene; edit them in the Inspector.
[CreateAssetMenu(menuName = "Matcha/Snack")]
public class Snack : ScriptableObject
{
    public string displayName;
    public Sprite sprite;
    [Tooltip("Must be heated in the microwave or toaster oven before serving")]
    public bool needsHeating;
    public int price = 3;
}

// How a snack came out. Cold-case snacks are None.
public enum Heat { None, Underheated, Warm, Burnt }
