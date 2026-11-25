using UnityEngine;
using UnityEngine.UI;

namespace Cloudborne
{
    /// <summary>
    /// Builds uGUI from code: rounded panels, rings, text.
    /// All sprites are generated at runtime, so no art files are needed.
    /// </summary>
    public static class UIKit
    {
        // ---------- Palette ----------
        public static readonly Color PanelDark = new Color(0.07f, 0.08f, 0.12f, 0.82f);

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
    }
}
