using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RacingGame.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace RacingGame.UI
{
    /// <summary>
    /// Builds uGUI from code: canvases, rounded panels, text, buttons, option selectors.
    /// All sprites are generated at runtime, so no art files are needed.
    /// </summary>
    public static class UIKit
    {
        // ---------- Palette ----------
        public static readonly Color Accent = new Color(1f, 0.78f, 0.1f);          // racing yellow
        public static readonly Color AccentHot = new Color(1f, 0.36f, 0.2f);       // orange-red
        public static readonly Color Nitro = new Color(0.3f, 0.75f, 1f);
        public static readonly Color Good = new Color(0.35f, 0.95f, 0.45f);
        public static readonly Color Bad = new Color(1f, 0.25f, 0.25f);
        public static readonly Color PanelDark = new Color(0.07f, 0.08f, 0.12f, 0.82f);
        public static readonly Color PanelLight = new Color(1f, 1f, 1f, 0.1f);
        public static readonly Color TextMain = Color.white;
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color ButtonDark = new Color(0.16f, 0.18f, 0.25f, 0.95f);

        public const float RefWidth = 1920f, RefHeight = 1080f;

        // ---------- Fonts ----------
        static Font titleFont, bodyFont;
        public static Font TitleFont => titleFont ? titleFont : titleFont = LoadFont("Fonts/RussoOne");
        public static Font BodyFont => bodyFont ? bodyFont : bodyFont = LoadFont("Fonts/RubikBold");

        static Font LoadFont(string path)
        {
            var f = Resources.Load<Font>(path);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ---------- Generated sprites ----------
        static Sprite rounded, roundedSmall, circle, ring, vignette, softGlow;
        public static Sprite Rounded => rounded ? rounded : rounded = MakeRounded(96, 30);
        public static Sprite RoundedSmall => roundedSmall ? roundedSmall : roundedSmall = MakeRounded(48, 14);
        public static Sprite Circle => circle ? circle : circle = MakeRing(256, 0f);
        public static Sprite Ring => ring ? ring : ring = MakeRing(256, 0.8f);
        public static Sprite Vignette => vignette ? vignette : vignette = MakeVignette(256);
        public static Sprite SoftGlow => softGlow ? softGlow : softGlow = MakeGlow(128);

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        /// <summary>innerRatio 0 = filled circle, 0.8 = ring with 20% thickness.</summary>
        static Sprite MakeRing(int size, float innerRatio)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float c = size * 0.5f, outer = c - 1f, inner = outer * innerRatio;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                float a = Mathf.Clamp01(outer - d + 0.5f);
                if (innerRatio > 0f) a *= Mathf.Clamp01(d - inner + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeVignette(int size)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2((x - c) / c, (y - c) / c).magnitude;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeGlow(int size)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2((x - c) / c, (y - c) / c).magnitude;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

        // ---------- Canvas & EventSystem ----------

        public static Canvas CreateCanvas(string name, Transform parent, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // landscape game: scale by height
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ---------- Layout helpers ----------

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            Color? color = null)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, size);
            return Image(rt, Rounded, color ?? PanelDark);
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter,
            bool title = false, bool shadow = true)
        {
            var rt = Stretch(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.font = title ? TitleFont : BodyFont;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.55f);
                s.effectDistance = new Vector2(2f, -3f);
            }
            return t;
        }

        public static Text Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 boxSize, TextAnchor align, bool title = false)
        {
            var holder = Rect(parent, "Label", anchor, pivot, pos, boxSize);
            return Label(holder, text, size, color, align, title);
        }

        // ---------- Buttons ----------

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            Action onClick, Color? color = null, Color? textColor = null, int fontSize = 40)
        {
            var rt = Rect(parent, "Button_" + label, anchor, pivot, pos, size);
            var img = Image(rt, Rounded, color ?? ButtonDark, true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            Label(rt, label, fontSize, textColor ?? TextMain, TextAnchor.MiddleCenter, true);
            rt.gameObject.AddComponent<ButtonFeedback>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>A row: label on the left, "&lt; value &gt;" on the right. Returns a setter to refresh the value.</summary>
        public static void OptionRow(Transform parent, string label, float y, float width, string[] options, int index,
            Action<int> onChanged)
        {
            var row = Rect(parent, "Option_" + label, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y),
                new Vector2(width, 84f));
            Image(row, Rounded, PanelLight);
            Label(row, "   " + label, 34, TextMain, TextAnchor.MiddleLeft);

            var valueBox = Rect(row, "Value", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-100f, 0f),
                new Vector2(260f, 70f));
            var valueText = Label(valueBox, options[index], 34, Accent, TextAnchor.MiddleCenter, true);

            int current = index;
            void Change(int delta)
            {
                current = (current + delta + options.Length) % options.Length;
                valueText.text = options[current];
                onChanged(current);
            }

            Button(row, "<", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-370f, 0f), new Vector2(70f, 64f),
                () => Change(-1), fontSize: 36);
            Button(row, ">", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(70f, 64f),
                () => Change(1), fontSize: 36);
        }

        // ---------- Icons ----------

        public static readonly Color Gold = new Color(1f, 0.8f, 0.2f);
        public static readonly Color Silver = new Color(0.8f, 0.84f, 0.9f);
        public static readonly Color Bronze = new Color(0.85f, 0.5f, 0.25f);

        /// <summary>Draws a trophy cup (bowl, handles, stem, base) filling the parent rect.</summary>
        public static void TrophyIcon(RectTransform parent, Color color)
        {
            var root = Stretch(parent, "Trophy");
            // Normalised layout inside the parent (fractions of its size).
            void Part(string name, Sprite sprite, float x, float y, float w, float h)
            {
                var rt = Stretch(root, name);
                rt.anchorMin = new Vector2(x - w * 0.5f, y - h * 0.5f);
                rt.anchorMax = new Vector2(x + w * 0.5f, y + h * 0.5f);
                Image(rt, sprite, color);
            }
            Part("HandleL", Ring, 0.27f, 0.66f, 0.26f, 0.26f);
            Part("HandleR", Ring, 0.73f, 0.66f, 0.26f, 0.26f);
            Part("Bowl", Circle, 0.5f, 0.62f, 0.5f, 0.5f);
            Part("Rim", RoundedSmall, 0.5f, 0.84f, 0.56f, 0.08f);
            Part("Stem", RoundedSmall, 0.5f, 0.32f, 0.1f, 0.2f);
            Part("Base", RoundedSmall, 0.5f, 0.17f, 0.46f, 0.1f);
        }

        // ---------- Formatting ----------

        public static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "TH";
            switch (n % 10)
            {
                case 1: return n + "ST";
                case 2: return n + "ND";
                case 3: return n + "RD";
                default: return n + "TH";
            }
        }

        public static string OrdinalSuffix(int n) => Ordinal(n).Substring(n.ToString().Length);

        public static string Money(int amount) => "$" + amount.ToString("N0");

        public static string Time(float t) => $"{(int)(t / 60f)}:{t % 60f:00.00}";

        public static void PlayClick() => ModelLibrary.PlayAt("impact", Vector3.zero, 0.25f, 2.2f, 0f);
    }

    /// <summary>Press-scale animation and click sound for buttons (works while the game is paused).</summary>
    public class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float target = 1f;

        public void OnPointerDown(PointerEventData e) { target = 0.92f; UIKit.PlayClick(); }
        public void OnPointerUp(PointerEventData e) => target = 1f;
        public void OnPointerExit(PointerEventData e) => target = 1f;

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Fades a CanvasGroup in (unscaled time) and scales it from slightly smaller.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FadeIn : MonoBehaviour
    {
        public float duration = 0.25f;
        float t;
        CanvasGroup group;

        void OnEnable()
        {
            group = GetComponent<CanvasGroup>();
            t = 0f;
            group.alpha = 0f;
            transform.localScale = Vector3.one * 0.95f;
        }

        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
            float e = 1f - Mathf.Pow(1f - t, 3f);
            group.alpha = e;
            transform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, e);
        }
    }

    /// <summary>"Punch" scale animation, e.g. for countdown numbers.</summary>
    public class Punch : MonoBehaviour
    {
        float t = 1f;
        float from = 1.6f;

        public void Play(float startScale = 1.6f) { from = startScale; t = 0f; }

        void Update()
        {
            if (t >= 1f) { transform.localScale = Vector3.one; return; }
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 0.35f);
            float e = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(from, 1f, e);
        }
    }
}
