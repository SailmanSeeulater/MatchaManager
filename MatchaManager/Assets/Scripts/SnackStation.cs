using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The prep station. Clicking a cold snack puts it on the tray. Clicking a hot
// snack picks it up; clicking the microwave or toaster oven puts it in to heat.
// Clicking the appliance again takes it out onto the tray: too early and it's
// still cold, too late and it's burnt. The tray holds one snack (a new one
// replaces it); click the tray to throw its snack away.
public class SnackStation : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public Button button;
        public Snack snack;
    }

    [Serializable]
    public class Appliance
    {
        public string name;
        public Button button;
        public TMP_Text timerText;
        public Image snackImage;
        [Tooltip("Seconds until the snack is warm")]
        public float readySeconds = 4;
        [Tooltip("Seconds until the snack burns")]
        public float burnSeconds = 8;

        [NonSerialized] public Snack snack;
        [NonSerialized] public float time;

        public Heat Heat => time < readySeconds ? Heat.Underheated : time < burnSeconds ? Heat.Warm : Heat.Burnt;
    }

    public Slot[] slots;
    public Appliance[] appliances;
    public Button trayButton;
    public Image trayImage;

    static readonly Color HeldTint = new Color(1f, 0.93f, 0.55f);
    static readonly Color BurntTint = new Color(0.35f, 0.25f, 0.2f);
    static readonly Color ColdTint = new Color(0.75f, 0.86f, 1f);

    public Snack Held { get; private set; }
    public Snack TraySnack { get; private set; }
    public Heat TrayHeat { get; private set; }

    // Fires when the tray changes.
    public event Action Changed;

    public Snack[] Menu => slots.Select(s => s.snack).Where(s => s != null).Distinct().ToArray();

    void Start()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].button.onClick.AddListener(() => ClickSlot(index));
        }
        for (int i = 0; i < appliances.Length; i++)
        {
            int index = i;
            appliances[i].button.onClick.AddListener(() => ClickAppliance(index));
        }
        trayButton.onClick.AddListener(TossTray);
        Refresh();
    }

    void Update() => Tick(Time.deltaTime);

    public void ClickSlot(int index)
    {
        var snack = slots[index].snack;
        if (snack == null) return;
        if (snack.needsHeating)
        {
            Held = Held == snack ? null : snack;
            SoundFX.Play(Sfx.Pick);
            Refresh();
            return;
        }
        Held = null;
        PutOnTray(snack, Heat.None);
    }

    public void ClickAppliance(int index)
    {
        var a = appliances[index];
        if (a.snack == null)
        {
            if (Held == null) return;
            a.snack = Held;
            a.time = 0;
            SoundFX.Play(Sfx.HeatStart);
            Held = null;
            Refresh();
            return;
        }
        var snack = a.snack;
        a.snack = null;
        PutOnTray(snack, a.Heat);
    }

    public void ClearTray()
    {
        TraySnack = null;
        TrayHeat = Heat.None;
        Refresh();
        Changed?.Invoke();
    }

    // Empties the appliances, the tray and your hand (start of a new day).
    public void ResetAll()
    {
        Held = null;
        foreach (var a in appliances) a.snack = null;
        ClearTray();
    }

    // Hands the tray's snack to a customer and empties the tray.
    public (Snack snack, Heat heat) TakeTray()
    {
        var plated = (TraySnack, TrayHeat);
        ClearTray();
        return plated;
    }

    public void Tick(float dt)
    {
        foreach (var a in appliances)
        {
            if (a.snack == null) continue;
            var before = a.Heat;
            a.time += dt;
            RefreshAppliance(a);
            if (a.Heat == before) continue;
            if (a.Heat == Heat.Warm) SoundFX.Play(Sfx.Ready);
            if (a.Heat == Heat.Burnt)
            {
                SoundFX.Play(Sfx.Burnt);
                Debug.Log($"[Snacks] {a.snack.displayName} burnt");
            }
        }
    }

    // Clicking the tray throws its snack away.
    public void TossTray()
    {
        if (TraySnack == null) return;
        SoundFX.Play(Sfx.Dump);
        ClearTray();
    }

    void PutOnTray(Snack snack, Heat heat)
    {
        SoundFX.Play(Sfx.Tray);
        TraySnack = snack;
        TrayHeat = heat;
        Refresh();
        Changed?.Invoke();
    }

    void Refresh()
    {
        foreach (var slot in slots)
        {
            var img = slot.button.GetComponent<Image>();
            img.color = slot.snack != null && slot.snack == Held ? HeldTint : Color.white;
        }
        foreach (var a in appliances) RefreshAppliance(a);

        trayImage.enabled = TraySnack != null;
        if (TraySnack != null)
        {
            trayImage.sprite = TraySnack.sprite;
            trayImage.SetNativeSize();
            trayImage.color = Tint(TrayHeat);
        }
    }

    void RefreshAppliance(Appliance a)
    {
        a.snackImage.enabled = a.snack != null;
        if (a.snack == null)
        {
            // hint while holding a hot snack
            a.timerText.text = Held != null ? "PUT IN" : "";
            return;
        }
        a.snackImage.sprite = a.snack.sprite;
        a.snackImage.SetNativeSize();
        a.snackImage.color = a.Heat == Heat.Burnt ? BurntTint : Color.white;
        a.timerText.text = a.Heat switch
        {
            Heat.Underheated => Mathf.CeilToInt(a.readySeconds - a.time).ToString(),
            Heat.Warm => "<color=#a6cf7a>READY</color>",
            _ => "<color=#d9705a>BURNT</color>",
        };
    }

    static Color Tint(Heat heat) => heat switch
    {
        Heat.Underheated => ColdTint,
        Heat.Burnt => BurntTint,
        _ => Color.white,
    };
}
