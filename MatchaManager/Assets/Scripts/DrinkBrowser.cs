using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Browses the drink tree like a file system: the 5 slots show the current
// folder's children. Clicking a folder opens it, clicking an ingredient adds
// one portion to the cup, Back goes up a level.
// Keyboard extras (KeyboardShortcuts): a digit that pours goes back HOME, ready for the
// next line; Z pours the last ingredient again; SHIFT+line jumps to a recipe line.
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
    DrinkNode lastPoured;
    bool fromKeyboard;

    public DrinkNode Current => path[path.Count - 1];

    void Start()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].onClick.AddListener(() => ClickSlot(index));
        }
        backButton.onClick.AddListener(Back);
        cup.Served += _ => { lastPoured = null; ShowRoot(); };
        cup.Dumped += () => lastPoured = null;
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
            if (Pour(node) && fromKeyboard) ShowRoot();
            return;
        }
        path.Add(node);
        SoundFX.Play(Sfx.Click);
        page = 0;
        Refresh();
    }

    bool Pour(DrinkNode node)
    {
        if (!cup.Add(node)) return false;
        lastPoured = node;
        return true;
    }

    // A slot pressed with its number key: like a click, but pouring goes back HOME.
    public void PressSlotKey(Button slot)
    {
        fromKeyboard = true;
        try { KeyboardShortcuts.Press(slot); }
        finally { fromKeyboard = false; }
    }

    // Z: one more portion of whatever was poured last (EXTRA without walking back to it).
    public void PourAgain()
    {
        if (lastPoured != null) Pour(lastPoured);
    }

    // SHIFT+line: an "any" line opens its folder so the player still picks which one, a WITH
    // line opens the folder its ingredient is in (picking it is the point), a named ingredient pours.
    public void JumpTo(Order.Line line)
    {
        if (line == null || line.removed) return;
        if (line.IsAny) OpenFolder(line.node);
        else if (line.folder != null) OpenFolder(Order.ParentOf(line.folder, line.node));
        else
        {
            Pour(line.node);
            ShowRoot();
        }
    }

    public void OpenFolder(DrinkNode folder)
    {
        var route = Route(root, folder);
        if (route == null) return;
        path.Clear();
        path.AddRange(route);
        page = 0;
        SoundFX.Play(Sfx.Click);
        Refresh();
    }

    // root ... target, or null when target isn't in the tree
    static List<DrinkNode> Route(DrinkNode from, DrinkNode target)
    {
        if (from == target) return new List<DrinkNode> { from };
        foreach (var kid in from.children)
        {
            if (kid.IsIngredient) continue;
            var rest = Route(kid, target);
            if (rest != null) { rest.Insert(0, from); return rest; }
        }
        return null;
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
                    ? $"{node.displayName.ToUpper()}\n{CostLabel(node)}"
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

    // "$2": what a portion costs
    string CostLabel(DrinkNode node) => $"<color=#8a2e2c>${cup.CostOf(node)}</color>";

    void ShowIcon(int slot, Sprite icon)
    {
        if (slotIcons == null || slot >= slotIcons.Length) return;
        slotIcons[slot].sprite = icon;
        slotIcons[slot].enabled = icon != null;
    }

    // "HOME > DRINK TYPE > TEA"; when that's too long for the bar, the start is
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
