using UnityEngine;

// Every number that makes the game easier or harder, in one asset.
// Defaults were tuned with the simulator (Oct 2026): beginners do fine for the
// first few days, regular players hit the goal all week but start losing
// customers by day 10, experts start feeling it around day 10.
//
// Assets/Data/Balance.asset (created by Matcha > Build Scene, never overwritten).
// Edit it in the Inspector, even during Play mode, and check the effect with
// Matcha > Simulate Balance, which plays whole days with bot players.
[CreateAssetMenu(menuName = "Matcha/Balance")]
public class Balance : ScriptableObject
{
    [Tooltip("Shown on the difficulty buttons and the end-of-day summary")]
    public string difficultyName = "MEDIUM";
    public string description = "THE REAL CAFE";

    [Header("Day")]
    [Tooltip("Real seconds from opening to closing")]
    public float realSecondsPerDay = 300;
    public int openHour = 9;
    public int closeHour = 17;
    [Tooltip("Happy customers to aim for on day 1 (the dots in the status bar fill toward it)")]
    public int dailyGoal = 8;
    [Tooltip("The goal grows by this much each day")]
    public int goalPerDay = 1;
    [Tooltip("...up to this")]
    public int maxGoal = 30;

    [Header("Customers")]
    [Tooltip("Seconds between new customers walking in (random in this range)")]
    public Vector2 arrivalInterval = new Vector2(14, 24);
    [Tooltip("Multiplies every recipe's patience (Patience Seconds on each recipe)")]
    public float patienceMultiplier = 1.3f;
    [Tooltip("Extra seconds a customer waits when they ordered a hot snack")]
    public float hotSnackExtraPatience = 10;
    [Tooltip("How fast patience drains in line compared to at pickup")]
    public float linePatienceRate = 0.4f;
    [Tooltip("Seconds the order shows at the counter before they move to pickup")]
    public float orderSeconds = 2;

    [Header("Orders (how finicky)")]
    [Range(0, 1)] public float snackChance = 0.4f;
    [Tooltip("Chance of a change to the recipe (NO / EXTRA / WITH / ADD) on day 1. Rolled again for each further change, so several changes are rarer")]
    [Range(0, 1)] public float changeChance = 0.5f;
    [Tooltip("Change chance grows by this each day...")]
    public float changeChancePerDay = 0.04f;
    [Tooltip("...up to this")]
    [Range(0, 1)] public float maxChangeChance = 0.8f;
    [Tooltip("Most changes one order can have")]
    public int maxChanges = 2;
    [Tooltip("How often a change ADDs something the recipe doesn't have (sugar, syrup, ice)")]
    [Range(0, 1)] public float addChance = 0.2f;
    [Tooltip("Ingredients this small (cup pixels) can be ADDed: sweeteners, syrups, ice")]
    public int addMaxPixels = 4;
    [Tooltip("How often an EXTRA is a double (x2)")]
    [Range(0, 1)] public float doubleExtraChance = 0.1f;

    [Header("Unclear orders")]
    [Tooltip("Chance a customer only says part of the drink's name (\"??? MATCHA\") on day 1. 0 = never")]
    [Range(0, 1)] public float unclearChance = 0;
    [Tooltip("...growing by this each day...")]
    public float unclearChancePerDay = 0;
    [Tooltip("...up to this")]
    [Range(0, 1)] public float maxUnclearChance = 0;
    [Tooltip("A wrong guess takes this much patience from everyone waiting (0.1 = 10%)")]
    public float wrongGuessPenalty = 0.1f;
    [Tooltip("Making an unclear order right without asking adds this much of the price as a bonus")]
    public float guessBonus = 0.5f;
    [HideInInspector] public int presetVersion; // lets Build Scene add new preset settings to older assets

    [Header("Costs")]
    [Tooltip("Coins a new game starts with, to pay for the first ingredients")]
    public int startingCoins = 20;
    [Tooltip("Multiplies every ingredient and snack cost")]
    public float costMultiplier = 1;

    [Header("Mistakes")]
    [Tooltip("Dumping the cup takes this much patience from every waiting customer (0.1 = 10%)")]
    public float dumpPenalty = 0.1f;

    [Header("Money")]
    [Tooltip("Tip at full patience, as a fraction of the price (1 = doubles the pay)")]
    public float tipMultiplier = 1;

    [Header("Each new day")]
    [Tooltip("Customers are this much less patient, and come this much more often, each day (0.05 = 5%)")]
    public float harderPerDay = 0.07f;
    [Tooltip("...until patience and time between customers reach this fraction of day 1")]
    public float hardestAt = 0.4f;

    [Header("Snacks (seconds)")]
    public float microwaveReady = 4;
    public float microwaveBurn = 8;
    public float ovenReady = 7;
    public float ovenBurn = 16;

    public float UnclearChance(int day) =>
        unclearChance <= 0 ? 0 : Mathf.Min(maxUnclearChance, unclearChance + unclearChancePerDay * (day - 1));

    public float ChangeChance(int day) => Mathf.Min(maxChangeChance, changeChance + changeChancePerDay * (day - 1));

    public int Goal(int day) => Mathf.Min(maxGoal, dailyGoal + goalPerDay * (day - 1));

    // 1 on day 1, shrinking each day down to hardestAt
    public float Hardness(int day) => Mathf.Max(hardestAt, 1 - harderPerDay * (day - 1));
}
