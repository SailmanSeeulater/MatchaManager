using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The drink being made. Each added portion stacks a colored layer inside the
// glass (contents is masked to the glass shape). Serve hands the drink off and empties the cup;
// clicking the glass pours it out.
public class Cup : MonoBehaviour
{
    public RectTransform contents;
    public Button serveButton;
    public Button dumpButton;
    public TMP_Text readout;

    readonly List<DrinkNode> added = new List<DrinkNode>();
    readonly List<GameObject> layers = new List<GameObject>();
    int filledPixels;

    // Ingredient -> number of portions. Customers will compare this to their order.
    public event Action<Dictionary<DrinkNode, int>> Served;
    // Fires whenever the contents change.
    public event Action Changed;

    public bool IsEmpty => added.Count == 0;

    // Serve only works while someone is at the counter.
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
        if (dumpButton != null) dumpButton.onClick.AddListener(Clear);
        Refresh();
    }

    // Returns false when the portion doesn't fit.
    public bool Add(DrinkNode ingredient)
    {
        if (filledPixels + ingredient.cupPixels > Capacity)
        {
            readout.text = Describe() + "\n<color=#b8423f>CUP IS FULL</color>";
            return false;
        }
        added.Add(ingredient);

        var layer = new GameObject(ingredient.displayName, typeof(RectTransform), typeof(Image));
        var rt = layer.GetComponent<RectTransform>();
        rt.SetParent(contents, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, filledPixels);
        rt.sizeDelta = new Vector2(0, ingredient.cupPixels);
        var img = layer.GetComponent<Image>();
        img.color = ingredient.cupColor;
        img.raycastTarget = false;
        layers.Add(layer);
        filledPixels += ingredient.cupPixels;

        Refresh();
        return true;
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
        Debug.Log("[Cup] Served: " + Describe().Replace("\n", ", "));
        Clear();
        Served?.Invoke(drink);
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

    // e.g. "CEREMONIAL MATCHA\nCANE SUGAR x2", in the order things were first added
    string Describe()
    {
        var counts = Counts();
        return string.Join("\n", added.Distinct().Select(n =>
            counts[n] > 1 ? $"{n.displayName.ToUpper()} x{counts[n]}" : n.displayName.ToUpper()));
    }
}
