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
                slotLabels[i].text = $"MORE <size=70%>{page + 1}/{PageCount}</size>";
            }
            else if (i < entries.Count)
            {
                var node = entries[i];
                slots[i].interactable = true;
                slotLabels[i].text = node.IsIngredient
                    ? $"{node.displayName.ToUpper()}\n<size=75%><color=#3f6b2e>+{node.PortionLabel.ToUpper()}</color></size>"
                    : node.displayName.ToUpper();
            }
            else
            {
                slots[i].interactable = false;
                slotLabels[i].text = "";
            }
        }
        backButton.interactable = path.Count > 1;
        pathText.text = string.Join(" > ", path.Select((n, i) => i == 0 ? "HOME" : n.displayName.ToUpper()));
    }
}
