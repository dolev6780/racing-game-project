using UnityEngine;
using UnityEngine.UI;
using RacingGame.Core;
using RacingGame.Garage;

namespace RacingGame.UI
{
    /// <summary>
    /// Garage: browse vehicles (3D preview in the menu backdrop), buy them, select one,
    /// and buy upgrades for the vehicles you own.
    /// </summary>
    public class GarageScreen : MonoBehaviour
    {
        MainMenu menu;
        int index;
        RectTransform panel;

        // Stat bar scales (roughly the best possible value of each stat).
        const float MaxSpeed = 46f, MaxAccel = 33f, MaxTurn = 215f, MaxArmor = 200f, MaxFirepower = 8f * 37f;

        public void Init(MainMenu owner)
        {
            menu = owner;
            index = VehicleCatalog.Vehicles.IndexOf(PlayerGarage.Selected);
            if (index < 0) index = 0;

            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            gameObject.AddComponent<CanvasGroup>();
            gameObject.AddComponent<FadeIn>();

            UIKit.Label(rt, "GARAGE", 90, UIKit.Accent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -30f),
                new Vector2(800f, 120f), TextAnchor.MiddleLeft, true);
            UIKit.Button(rt, "BACK", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(50f, 40f), new Vector2(300f, 100f),
                () => menu.CloseGarage(), fontSize: 44);

            Rebuild();
        }

        VehicleDef Current => VehicleCatalog.Vehicles[index];

        void Browse(int delta)
        {
            int n = VehicleCatalog.Vehicles.Count;
            index = (index + delta + n) % n;
            Rebuild();
        }

        void Rebuild()
        {
            if (panel) Destroy(panel.gameObject);
            var v = Current;
            menu.ShowcaseVehicle(v);
            menu.RefreshBank();

            bool owned = PlayerGarage.Owns(v);
            bool selected = PlayerGarage.Selected == v;
            var stats = owned ? PlayerGarage.Stats(v) : VehicleCatalog.ComputeStats(v, 0, 0, 0, 0, 0);

            panel = UIKit.Panel(transform, "GaragePanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f),
                new Vector2(940f, 1040f)).rectTransform;
            var top = new Vector2(0.5f, 1f);

            // --- Header: browse arrows + name ---
            UIKit.Button(panel, "<", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(100f, 100f),
                () => Browse(-1), fontSize: 56);
            UIKit.Button(panel, ">", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(100f, 100f),
                () => Browse(1), fontSize: 56);
            UIKit.Label(panel, v.Name, 76, UIKit.TextMain, top, top, new Vector2(0f, -22f), new Vector2(600f, 90f),
                TextAnchor.MiddleCenter, true);
            UIKit.Label(panel, $"{index + 1} / {VehicleCatalog.Vehicles.Count}", 26, UIKit.TextDim, top, top,
                new Vector2(0f, -104f), new Vector2(300f, 34f), TextAnchor.MiddleCenter);

            var desc = UIKit.Label(panel, v.Description, 28, UIKit.TextDim, top, top, new Vector2(0f, -146f),
                new Vector2(860f, 70f), TextAnchor.MiddleCenter);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            // --- Stats ---
            float y = -225f;
            StatBar("SPEED", stats.TopSpeed / MaxSpeed, ref y);
            StatBar("ACCEL", stats.Acceleration / MaxAccel, ref y);
            StatBar("HANDLING", stats.TurnSpeed / MaxTurn, ref y);
            StatBar("ARMOR", stats.Armor / MaxArmor, ref y);
            StatBar("FIREPOWER", stats.Missiles * stats.MissileDamage / MaxFirepower, ref y);
            UIKit.Label(panel, $"PER LAP:  {stats.Missiles} MISSILES   {stats.Mines} MINES   {stats.Boosts} NITRO", 26,
                UIKit.TextMain, top, top, new Vector2(0f, y - 4f), new Vector2(860f, 36f), TextAnchor.MiddleCenter);
            y -= 48f;

            // --- Upgrades (owned) or purchase (not owned) ---
            if (owned)
            {
                for (int i = 0; i < VehicleCatalog.UpgradeNames.Length; i++)
                    UpgradeRow(v, (UpgradeType)i, ref y);
            }
            else
            {
                UIKit.Label(panel, "UPGRADES UNLOCK AFTER PURCHASE", 30, UIKit.TextDim, top, top, new Vector2(0f, y - 60f),
                    new Vector2(860f, 50f), TextAnchor.MiddleCenter);
            }

            // --- Main action ---
            var bottom = new Vector2(0.5f, 0f);
            var size = new Vector2(560f, 90f);
            var dark = new Color(0.1f, 0.1f, 0.12f);
            if (!owned)
            {
                bool canAfford = Wallet.Money >= v.Price;
                var buy = UIKit.Button(panel, canAfford ? $"BUY  {UIKit.Money(v.Price)}" : $"NEED  {UIKit.Money(v.Price)}",
                    bottom, bottom, new Vector2(0f, 20f), size, () =>
                    {
                        if (PlayerGarage.Buy(v)) Celebrate();
                        Rebuild();
                    }, UIKit.Accent, dark, 46);
                buy.interactable = canAfford;
            }
            else if (!selected)
            {
                UIKit.Button(panel, "SELECT", bottom, bottom, new Vector2(0f, 20f), size, () =>
                {
                    PlayerGarage.Select(v);
                    Rebuild();
                }, UIKit.Accent, dark, 48);
            }
            else
            {
                var sel = UIKit.Button(panel, "SELECTED FOR RACING", bottom, bottom, new Vector2(0f, 20f), size, null,
                    UIKit.Good, dark, 40);
                sel.interactable = false;
            }
        }

        void StatBar(string label, float value01, ref float y)
        {
            var top = new Vector2(0.5f, 1f);
            var row = UIKit.Rect(panel, "Stat_" + label, top, top, new Vector2(0f, y), new Vector2(860f, 40f));
            UIKit.Label(row, label, 28, UIKit.TextMain, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(260f, 40f), TextAnchor.MiddleLeft);

            var track = UIKit.Rect(row, "Track", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(580f, 22f));
            UIKit.Image(track, UIKit.RoundedSmall, new Color(1f, 1f, 1f, 0.12f));
            var fill = UIKit.Stretch(track, "Fill");
            fill.anchorMax = new Vector2(Mathf.Clamp(value01, 0.03f, 1f), 1f);
            UIKit.Image(fill, UIKit.RoundedSmall, Color.Lerp(UIKit.Accent, UIKit.AccentHot, value01));
            y -= 40f;
        }

        void UpgradeRow(VehicleDef v, UpgradeType type, ref float y)
        {
            var top = new Vector2(0.5f, 1f);
            int level = PlayerGarage.Level(v, type);
            int price = VehicleCatalog.UpgradePrice(type, level);

            var row = UIKit.Rect(panel, "Upgrade_" + type, top, top, new Vector2(0f, y), new Vector2(880f, 72f));
            UIKit.Image(row, UIKit.Rounded, UIKit.PanelLight);
            UIKit.Label(row, VehicleCatalog.UpgradeNames[(int)type], 32, UIKit.TextMain, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 10f), new Vector2(260f, 40f), TextAnchor.MiddleLeft, true);
            UIKit.Label(row, VehicleCatalog.UpgradeEffects[(int)type], 20, UIKit.TextDim, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, -20f), new Vector2(520f, 28f), TextAnchor.MiddleLeft);

            // Level pips
            for (int i = 0; i < VehicleCatalog.MaxUpgradeLevel; i++)
            {
                var pip = UIKit.Rect(row, "Pip", new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-370f + i * 34f, 0f),
                    new Vector2(24f, 24f));
                UIKit.Image(pip, UIKit.Circle, i < level ? UIKit.Accent : new Color(1f, 1f, 1f, 0.18f));
            }

            bool maxed = price < 0;
            bool canAfford = !maxed && Wallet.Money >= price;
            var btn = UIKit.Button(row, maxed ? "MAX" : UIKit.Money(price), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(200f, 60f), () =>
                {
                    if (PlayerGarage.Upgrade(v, type)) Celebrate();
                    Rebuild();
                }, canAfford ? UIKit.Accent : UIKit.ButtonDark, canAfford ? new Color(0.1f, 0.1f, 0.12f) : UIKit.TextDim, 32);
            btn.interactable = canAfford;
            y -= 80f;
        }

        static void Celebrate()
        {
            ModelLibrary.PlayAt("impact", Vector3.zero, 0.6f, 1.6f, 0f);
        }
    }
}
