using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Browses the drink tree like a file system: the 5 slots show the current
// folder's children. Clicking a folder opens it, clicking an ingredient adds
// one portion to the cup, Back goes up a level.
public class DrinkBrowser : MonoBehaviour
{
    public DrinkNode root;
    public Button[] slots;
    public TMP_Text[] slotLabels;
    public Image[] slotIcons;
    public Sprite moreIcon;
    public Button backButton;
    public TMP_Text pathText;
    public Cup cup;

    readonly List<DrinkNode> path = new List<DrinkNode>(); // root ... current folder
    int page;

    public DrinkNode Current => path[path.Count - 1];

    void Start()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].onClick.AddListener(() => ClickSlot(index));
        }
        backButton.onClick.AddListener(Back);
        cup.Served += _ => ShowRoot();
        ShowRoot();
    }

    public void ShowRoot()
    {
        path.Clear();
        path.Add(root);
        page = 0;
        Refresh();
    }

    public void ClickSlot(int index)
    {
        if (IsMoreSlot(index))
        {
            page = (page + 1) % PageCount;
            SoundFX.Play(Sfx.Click);
            Refresh();
            return;
        }
        var entries = VisibleEntries();
        if (index >= entries.Count) return;

        var node = entries[index];
        if (node.IsIngredient)
        {
            cup.Add(node);
            return;
        }
        path.Add(node);
        SoundFX.Play(Sfx.Click);
        page = 0;
        Refresh();
    }

    public void Back()
    {
        if (path.Count <= 1) return;
        path.RemoveAt(path.Count - 1);
        page = 0;
        Refresh();
    }

    // Folders with more children than slots get paged: the last slot becomes "MORE".
    bool Paged => Current.children.Count > slots.Length;
    int PerPage => Paged ? slots.Length - 1 : slots.Length;
    int PageCount => Mathf.CeilToInt(Current.children.Count / (float)PerPage);
    bool IsMoreSlot(int index) => Paged && index == slots.Length - 1;

    public List<DrinkNode> VisibleEntries() => Current.children.Skip(page * PerPage).Take(PerPage).ToList();

    void Refresh()
    {
        var entries = VisibleEntries();
        for (int i = 0; i < slots.Length; i++)
        {
            if (IsMoreSlot(i))
            {
                slots[i].interactable = true;
                slotLabels[i].text = $"MORE\n{page + 1}/{PageCount}";
                ShowIcon(i, moreIcon);
            }
            else if (i < entries.Count)
            {
                var node = entries[i];
                slots[i].interactable = true;
                ShowIcon(i, node.icon);
                slotLabels[i].text = node.IsIngredient
                    ? $"{node.displayName.ToUpper()}\n<color=#3f6b2e>+{node.PortionLabel.ToUpper()}</color>"
                    : node.displayName.ToUpper();
            }
            else
            {
                slots[i].interactable = false;
                slotLabels[i].text = "";
                ShowIcon(i, null);
            }
        }
        backButton.interactable = path.Count > 1;
        pathText.text = PathLabel();
    }

    void ShowIcon(int slot, Sprite icon)
    {
        if (slotIcons == null || slot >= slotIcons.Length) return;
        slotIcons[slot].sprite = icon;
        slotIcons[slot].enabled = icon != null;
    }

    // "HOME > FLAVORS > TEA"; when that's too long for the bar, the start is
    // dropped instead of the end so you always see where you are: "... > TEA > MATCHA".
    string PathLabel()
    {
        const int maxChars = 24;
        var names = path.Select((n, i) => i == 0 ? "HOME" : n.displayName.ToUpper()).ToList();
        var label = string.Join(" > ", names);
        while (label.Length > maxChars && names.Count > 1)
        {
            names.RemoveAt(0);
            label = "... > " + string.Join(" > ", names);
        }
        return label;
    }
}
