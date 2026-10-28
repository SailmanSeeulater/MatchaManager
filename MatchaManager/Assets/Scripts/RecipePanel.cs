using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The bottom-right notebook. RECIPE shows the current order as a checklist
// (filled in by CustomerManager). MENU lists every drink and snack with its
// price; click one to read how it's made, click the title to go back.
public class RecipePanel : MonoBehaviour
{
    public enum Tab { Menu, Recipe }

    public Button menuTab;
    public Button recipeTab;
    public TMP_Text title;
    public Button titleButton;   // "back" while reading a menu item
    public TMP_Text body;

    [Header("Menu list")]
    public TMP_Text drinkHeader;
    public TMP_Text snackHeader;
    public Button[] drinkRows;
    public Button[] snackRows;

    public CustomerManager customers;

    static readonly Color TabActive = Color.white;
    static readonly Color TabInactive = new Color32(0xd8, 0xcc, 0xb0, 0xff);

    Tab tab = Tab.Recipe;
    Object detail; // DrinkRecipe or Snack being read on the menu, null = the list
    string orderTitle = "NO ORDER YET";
    string orderBody = "WAITING FOR A CUSTOMER...";

    public Tab Current => tab;
    public Object Detail => detail;

    List<DrinkRecipe> Drinks => customers.recipes;
    Snack[] Snacks => customers.snacks != null ? customers.snacks.Menu : new Snack[0];

    void Start()
    {
        menuTab.onClick.AddListener(() => ShowTab(Tab.Menu));
        recipeTab.onClick.AddListener(() => ShowTab(Tab.Recipe));
        titleButton.onClick.AddListener(BackToMenu);
        for (int i = 0; i < drinkRows.Length; i++)
        {
            int index = i;
            drinkRows[i].onClick.AddListener(() => ClickDrink(index));
        }
        for (int i = 0; i < snackRows.Length; i++)
        {
            int index = i;
            snackRows[i].onClick.AddListener(() => ClickSnack(index));
        }
        Refresh();
    }

    public void ShowTab(Tab t)
    {
        tab = t;
        detail = null;
        Refresh();
    }

    // Called by CustomerManager whenever the order or the cup/tray changes.
    public void SetOrder(string orderTitle, string orderBody)
    {
        this.orderTitle = orderTitle;
        this.orderBody = orderBody;
        if (tab == Tab.Recipe) Refresh();
    }

    public void ClickDrink(int index)
    {
        if (index >= Drinks.Count) return;
        detail = Drinks[index];
        Refresh();
    }

    public void ClickSnack(int index)
    {
        if (index >= Snacks.Length) return;
        detail = Snacks[index];
        Refresh();
    }

    public void BackToMenu()
    {
        if (tab != Tab.Menu || detail == null) return;
        detail = null;
        Refresh();
    }

    void Refresh()
    {
        menuTab.image.color = tab == Tab.Menu ? TabActive : TabInactive;
        recipeTab.image.color = tab == Tab.Recipe ? TabActive : TabInactive;
        bool list = tab == Tab.Menu && detail == null;
        drinkHeader.gameObject.SetActive(list);
        snackHeader.gameObject.SetActive(list);
        FillRows(drinkRows, list ? Drinks.Select(d => (d.displayName.ToUpper(), d.price)).ToList() : null);
        FillRows(snackRows, list ? Snacks.Select(s => ((s.needsHeating ? "HOT " : "") + s.displayName.ToUpper(), s.price)).ToList() : null);

        if (tab == Tab.Recipe)
        {
            title.text = orderTitle;
            body.text = orderBody;
        }
        else if (detail == null)
        {
            title.text = "MENU";
            body.text = "";
        }
        else if (detail is DrinkRecipe drink)
        {
            title.text = "< " + drink.displayName.ToUpper();
            body.text = DescribeDrink(drink);
        }
        else if (detail is Snack snack)
        {
            title.text = "< " + snack.displayName.ToUpper();
            body.text = DescribeSnack(snack);
        }
    }

    // Rows past the end of the list (or all of them when entries is null) are hidden.
    static void FillRows(Button[] rows, List<(string name, int price)> entries)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            bool show = entries != null && i < entries.Count;
            rows[i].gameObject.SetActive(show);
            if (!show) continue;
            ((TMP_Text)rows[i].targetGraphic).text = entries[i].name;
            rows[i].transform.Find("Price").GetComponent<TMP_Text>().text = entries[i].price.ToString();
        }
    }

    static string DescribeDrink(DrinkRecipe drink)
    {
        var used = drink.lines.Where(l => l.node != null).ToList();
        var lines = new Order(drink).lines;
        var rows = lines.Select((line, i) =>
            $"- {line.Name} {line.Amount}" + (used[i].optional ? " <color=#8a7a5a>(OPTIONAL)</color>" : "")).ToList();
        rows.Add($"<color=#2e5222>PRICE {drink.price}</color>  WAITS {drink.patienceSeconds:0}S");
        return string.Join("\n", rows);
    }

    string DescribeSnack(Snack snack)
    {
        var rows = new List<string>();
        if (snack.needsHeating && customers.snacks != null)
        {
            rows.Add("HEAT IT BEFORE SERVING:");
            rows.AddRange(customers.snacks.appliances.Select(a =>
                $"- {a.name}: READY {a.readySeconds:0}S, BURNS {a.burnSeconds:0}S"));
        }
        else
        {
            rows.Add("FROM THE COLD CASE,");
            rows.Add("STRAIGHT ONTO THE TRAY");
        }
        rows.Add($"<color=#2e5222>PRICE {snack.price}</color>");
        return string.Join("\n", rows);
    }
}
