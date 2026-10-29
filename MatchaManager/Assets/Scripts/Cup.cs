using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The drink being made. Each added portion stacks a layer inside the glass
// (contents is masked to the glass shape): liquids are smooth with a lighter
// surface line, powders are grainy, and ice adds cubes that float in front of
// everything. Serve hands the drink off and empties the cup; clicking the glass pours it out.
public class Cup : MonoBehaviour
{
    public RectTransform contents;
    public Button serveButton;
    public Button dumpButton;
    public TMP_Text readout;

    [Header("Solid ingredients")]
    public Sprite powderGrain;      // tiled over powder layers
    public Sprite iceCube;
    public RectTransform iceFront;  // cubes go here so later layers never cover them
    public int cubesPerPortion = 2;

    readonly List<DrinkNode> added = new List<DrinkNode>();
    readonly List<GameObject> layers = new List<GameObject>();
    int filledPixels;

    // Ingredient -> number of portions. Customers will compare this to their order.
    public event Action<Dictionary<DrinkNode, int>> Served;
    // Asked before serving: who takes this drink? Returns false if nobody ordered it,
    // and then the drink stays in the cup. Null = anyone takes anything.
    public Func<Dictionary<DrinkNode, int>, bool> Recipient;
    // Fires whenever the contents change.
    public event Action Changed;

    public bool IsEmpty => added.Count == 0;

    // Serve only works while someone is waiting for a drink.
    bool hasCustomer = true;
    public bool HasCustomer
    {
        get => hasCustomer;
        set { hasCustomer = value; Refresh(); }
    }
    int Capacity => Mathf.RoundToInt(contents.rect.height);

    void Awake()
    {
        serveButton.onClick.AddListener(Serve);
        if (dumpButton != null) dumpButton.onClick.AddListener(PourOut);
        Refresh();
    }

    // Returns false when the portion doesn't fit.
    public bool Add(DrinkNode ingredient)
    {
        if (filledPixels + ingredient.cupPixels > Capacity)
        {
            readout.text = Describe() + "\n<color=#b8423f>CUP IS FULL</color>";
            SoundFX.Play(Sfx.CupFull);
            return false;
        }
        added.Add(ingredient);

        var color = ingredient.cupColor;
        var layer = Band(contents, ingredient.displayName, filledPixels, ingredient.cupPixels, color);
        layers.Add(layer.gameObject);
        switch (ingredient.look)
        {
            case CupLook.Powder:
                // darker grains over the color
                var grains = Band(layer, "Grains", 0, ingredient.cupPixels, Color.Lerp(color, Color.black, 0.25f));
                grains.anchorMax = Vector2.one;
                grains.sizeDelta = Vector2.zero;
                var grainImage = grains.GetComponent<Image>();
                grainImage.sprite = powderGrain;
                grainImage.type = Image.Type.Tiled;
                SoundFX.Play(Sfx.Pick);
                break;
            case CupLook.Ice:
                // the layer is just a pale tint; the cubes are what you see
                layer.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.35f);
                AddIceCubes(filledPixels, ingredient.cupPixels);
                SoundFX.Play(Sfx.Tray);
                break;
            default:
                // a 1px lighter line on top reads as the liquid's surface
                Band(layer, "Surface", ingredient.cupPixels - 1, 1, Color.Lerp(color, Color.white, 0.35f));
                SoundFX.Play(Sfx.Pour);
                break;
        }
        filledPixels += ingredient.cupPixels;
        if (iceFront != null) iceFront.SetAsLastSibling();

        Refresh();
        return true;
    }

    // A strip across the glass, bottom-anchored at y with the given height.
    static RectTransform Band(Transform parent, string name, float y, float height, Color color)
    {
        var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, height);
        var img = rt.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // Cubes scattered across the glass around the ice layer, a little jumbled.
    void AddIceCubes(int y, int height)
    {
        if (iceCube == null || iceFront == null) return;
        float width = contents.rect.width;
        float size = iceCube.rect.width;
        for (int i = 0; i < cubesPerPortion; i++)
        {
            var cube = new GameObject("IceCube", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            cube.SetParent(iceFront, false);
            cube.anchorMin = cube.anchorMax = cube.pivot = Vector2.zero;
            // spread the cubes across the width, then nudge each one
            float slot = (width - size) / cubesPerPortion;
            cube.anchoredPosition = new Vector2(
                Mathf.Round(slot * i + UnityEngine.Random.Range(2f, Mathf.Max(2f, slot - 2f))),
                Mathf.Round(y + UnityEngine.Random.Range(-1f, Mathf.Max(0f, height - size + 2f))));
            cube.sizeDelta = new Vector2(size, size);
            var img = cube.GetComponent<Image>();
            img.sprite = iceCube;
            img.raycastTarget = false;
            layers.Add(cube.gameObject);
        }
    }

    public Dictionary<DrinkNode, int> Counts()
    {
        var counts = new Dictionary<DrinkNode, int>();
        foreach (var node in added) counts[node] = counts.TryGetValue(node, out var n) ? n + 1 : 1;
        return counts;
    }

    public void Serve()
    {
        if (IsEmpty || !hasCustomer) return;
        var drink = Counts();
        if (Recipient != null && !Recipient(drink))
        {
            readout.text = Describe() + "\n<color=#b8423f>NOBODY ORDERED THIS</color>";
            SoundFX.Play(Sfx.CupFull);
            return;
        }
        Debug.Log("[Cup] Served: " + Describe().Replace("\n", ", "));
        Clear();
        Served?.Invoke(drink);
    }

    // Clicking the glass: tip the drink out.
    public void PourOut()
    {
        if (IsEmpty) return;
        SoundFX.Play(Sfx.Dump);
        Clear();
    }

    public void Clear()
    {
        foreach (var layer in layers)
        {
            if (Application.isPlaying) Destroy(layer);
            else DestroyImmediate(layer);
        }
        layers.Clear();
        added.Clear();
        filledPixels = 0;
        Refresh();
    }

    void Refresh()
    {
        serveButton.interactable = !IsEmpty && hasCustomer;
        readout.text = Describe();
        Changed?.Invoke();
    }

    // e.g. "CEREMONIAL MATCHA, CANE SUGAR x2", in the order things were first added
    string Describe()
    {
        var counts = Counts();
        return string.Join(", ", added.Distinct().Select(n =>
            counts[n] > 1 ? $"{n.displayName.ToUpper()} x{counts[n]}" : n.displayName.ToUpper()));
    }
}
