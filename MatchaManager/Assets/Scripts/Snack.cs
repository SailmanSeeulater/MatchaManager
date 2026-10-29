using UnityEngine;

// Something from the display cases. Cold snacks go straight onto the tray;
// hot ones have to be heated in the microwave or toaster oven first.
// Defaults are created by Matcha > Build Scene; edit them in the Inspector.
[CreateAssetMenu(menuName = "Matcha/Snack")]
public class Snack : ScriptableObject
{
    public string displayName;
    [Tooltip("Short name for the small order tickets, e.g. CAKE (blank = the full name)")]
    public string ticketName;
    public Sprite sprite;
    [Tooltip("Must be heated in the microwave or toaster oven before serving")]
    public bool needsHeating;
    public int price = 3;
    [Tooltip("Coins it costs the shop when it leaves the case")]
    public int cost = 1;

    public string TicketName => string.IsNullOrEmpty(ticketName) ? displayName : ticketName;
}

// How a snack came out. Cold-case snacks are None.
public enum Heat { None, Underheated, Warm, Burnt }
