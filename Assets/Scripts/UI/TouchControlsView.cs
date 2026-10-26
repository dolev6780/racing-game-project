using UnityEngine;
using UnityEngine.UI;
using RacingGame.Car;
using RacingGame.Race;
using Btn = RacingGame.Car.PlayerCarInput.Btn;

namespace RacingGame.UI
{
    /// <summary>
    /// Draws the phone controls (arrow buttons or floating stick, GAS/REV pedals, action buttons with
    /// ammo badges, tilt meter). The input logic lives in PlayerCarInput; this only mirrors its layout and state.
    /// </summary>
    public class TouchControlsView : MonoBehaviour
    {
        class ButtonView
        {
            public Btn Id;
            public RectTransform Rect;
            public Image Image;
            public Color Color;
            public Text Badge;
            public GameObject BadgeRoot;
        }

        Canvas canvas;
        Racer player;
        PlayerCarInput input;
        RectTransform root;
        readonly ButtonView[] buttons = new ButtonView[(int)Btn.Count];

        RectTransform stickBase, stickKnob;
        Image stickBaseImage, stickKnobImage;
        RectTransform tiltBar, tiltKnob;

        public void Init(Transform parent, Canvas parentCanvas, Racer playerRacer)
        {
            canvas = parentCanvas;
            player = playerRacer;
            input = player.Car.GetComponent<PlayerCarInput>();

            root = (RectTransform)transform;
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            AddButton(Btn.Left, "<", UIKit.ButtonDark, false, false);
            AddButton(Btn.Right, ">", UIKit.ButtonDark, false, false);
            AddButton(Btn.Gas, "GAS", new Color(0.18f, 0.7f, 0.29f), false, false);
            AddButton(Btn.Reverse, "REV", new Color(0.85f, 0.28f, 0.23f), false, false);
            AddButton(Btn.Mine, "MINE", new Color(0.35f, 0.36f, 0.42f), true, true);
            AddButton(Btn.Boost, "NITRO", UIKit.Nitro, true, true);
            AddButton(Btn.Fire, "FIRE", UIKit.AccentHot, true, true);

            // Floating stick ("point where you want to go")
            stickBase = Node("StickBase", new Vector2(0.5f, 0.5f));
            stickBaseImage = UIKit.Image(stickBase, UIKit.Circle, new Color(1f, 1f, 1f, 0.15f));
            var ring = UIKit.Stretch(stickBase, "Ring");
            UIKit.Image(ring, UIKit.Ring, new Color(1f, 1f, 1f, 0.45f));
            stickKnob = Node("StickKnob", new Vector2(0.5f, 0.5f));
            stickKnobImage = UIKit.Image(stickKnob, UIKit.Circle, UIKit.Accent);

            // Tilt meter
            tiltBar = UIKit.Rect(root, "TiltBar", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(460f, 26f));
            UIKit.Image(tiltBar, UIKit.RoundedSmall, new Color(0f, 0f, 0f, 0.5f));
            tiltKnob = UIKit.Rect(tiltBar, "Knob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
            UIKit.Image(tiltKnob, UIKit.Circle, UIKit.Accent);
            UIKit.Label(tiltBar, "TILT", 22, UIKit.TextDim, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f),
                new Vector2(200f, 30f), TextAnchor.MiddleCenter);
        }

        RectTransform Node(string name, Vector2 pivot)
        {
            var rt = UIKit.Rect(root, name, new Vector2(0f, 1f), pivot, Vector2.zero, Vector2.one);
            return rt;
        }

        void AddButton(Btn id, string label, Color color, bool round, bool badge)
        {
            var rt = Node("Btn_" + id, new Vector2(0.5f, 0.5f));
            var img = UIKit.Image(rt, round ? UIKit.Circle : UIKit.Rounded, color);
            var inner = UIKit.Stretch(rt, "Ring");
            UIKit.Image(inner, round ? UIKit.Ring : null, new Color(1f, 1f, 1f, round ? 0.35f : 0f));
            UIKit.Label(rt, label, label.Length > 2 ? 32 : 56, UIKit.TextMain, TextAnchor.MiddleCenter, true);

            var view = new ButtonView { Id = id, Rect = rt, Image = img, Color = color };
            if (badge)
            {
                var b = UIKit.Rect(rt, "Badge", new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14f, -14f),
                    new Vector2(54f, 54f));
                UIKit.Image(b, UIKit.Circle, new Color(0.08f, 0.08f, 0.1f, 0.95f));
                view.Badge = UIKit.Label(b, "0", 30, UIKit.TextMain, TextAnchor.MiddleCenter, true);
                view.BadgeRoot = b.gameObject;
            }
            buttons[(int)id] = view;
        }

        void LateUpdate()
        {
            bool show = PlayerCarInput.ShowTouchControls && input != null && player.Car != null;
            if (!show)
            {
                // Every child is explicitly re-activated below when controls are shown again.
                foreach (Transform child in root) child.gameObject.SetActive(false);
                return;
            }

            float sf = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            var mode = PlayerCarInput.Steering;

            foreach (var b in buttons)
            {
                Rect r = input.ButtonRect(b.Id);
                bool visible = r.width > 0f;
                b.Rect.gameObject.SetActive(visible);
                if (!visible) continue;

                b.Rect.anchoredPosition = new Vector2(r.center.x / sf, -r.center.y / sf);
                b.Rect.sizeDelta = new Vector2(r.width / sf, r.height / sf);

                bool held = input.IsHeld(b.Id);
                int ammo = Ammo(b.Id);
                bool empty = ammo == 0;
                var c = b.Color;
                c.a = held ? 1f : empty ? 0.3f : 0.7f;
                b.Image.color = held ? Color.Lerp(c, Color.white, 0.25f) : c;
                float target = held ? 0.92f : 1f;
                float s = Mathf.Lerp(b.Rect.localScale.x, target, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
                b.Rect.localScale = new Vector3(s, s, 1f);

                if (b.Badge != null) b.Badge.text = ammo.ToString();
            }

            // Stick
            bool stick = mode == SteeringMode.Stick;
            stickBase.gameObject.SetActive(stick);
            stickKnob.gameObject.SetActive(stick);
            if (stick)
            {
                bool active = input.StickActive;
                Vector2 origin = input.StickOrigin, knob = input.StickKnob;
                float radius = input.StickRadius;

                stickBase.anchoredPosition = new Vector2(origin.x / sf, -origin.y / sf);
                stickBase.sizeDelta = Vector2.one * (radius * 2f / sf);
                stickKnob.anchoredPosition = new Vector2(knob.x / sf, -knob.y / sf);
                stickKnob.sizeDelta = Vector2.one * (radius * 0.84f / sf);
                stickBaseImage.color = new Color(1f, 1f, 1f, active ? 0.18f : 0.08f);
                var kc = UIKit.Accent;
                kc.a = active ? 0.95f : 0.4f;
                stickKnobImage.color = kc;
            }

            // Tilt meter
            bool tilt = mode == SteeringMode.Tilt;
            tiltBar.gameObject.SetActive(tilt);
            if (tilt) tiltKnob.anchoredPosition = new Vector2(input.TiltSteer * (tiltBar.sizeDelta.x * 0.5f - 20f), 0f);
        }

        int Ammo(Btn id)
        {
            var w = player.Weapons;
            switch (id)
            {
                case Btn.Fire: return w.Missiles;
                case Btn.Mine: return w.Mines;
                case Btn.Boost: return w.Boosts;
                default: return -1;
            }
        }
    }
}
