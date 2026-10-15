using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RacingGame.Core;
using RacingGame.Progress;

namespace RacingGame.UI
{
    /// <summary>
    /// Persistent overlay (survives menu/race switches) that pops up "ACHIEVEMENT UNLOCKED"
    /// banners, and runs the automatic achievement checks a few times per second.
    /// </summary>
    public class AchievementToast : MonoBehaviour
    {
        static AchievementToast instance;

        readonly Queue<AchievementDef> queue = new Queue<AchievementDef>();
        Canvas canvas;
        RectTransform current;
        float shownAt;
        float nextCheck;

        const float ShowTime = 3.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("AchievementToasts");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AchievementToast>();
        }

        void Awake()
        {
            canvas = UIKit.CreateCanvas("ToastCanvas", transform, 100);
            Achievements.Unlocked += OnUnlocked;
        }

        void OnDestroy() => Achievements.Unlocked -= OnUnlocked;

        void OnUnlocked(AchievementDef a) => queue.Enqueue(a);

        void Update()
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.4f;
                Achievements.CheckIfDirty();
            }

            if (current == null && queue.Count > 0) Show(queue.Dequeue());
            if (current == null) return;

            // Slide in from above, hold, slide out.
            float t = Time.unscaledTime - shownAt;
            float inT = Mathf.Clamp01(t / 0.35f), outT = Mathf.Clamp01((t - ShowTime) / 0.35f);
            float ease(float x) => 1f - Mathf.Pow(1f - x, 3f);
            float y = Mathf.Lerp(200f, -30f, ease(inT)) + Mathf.Lerp(0f, 230f, outT * outT);
            current.anchoredPosition = new Vector2(0f, y);
            if (outT >= 1f)
            {
                Destroy(current.gameObject);
                current = null;
            }
        }

        void Show(AchievementDef a)
        {
            var top = new Vector2(0.5f, 1f);
            var panel = UIKit.Panel(canvas.transform, "Toast", top, top, new Vector2(0f, 200f), new Vector2(860f, 140f),
                new Color(0.08f, 0.09f, 0.13f, 0.95f)).rectTransform;
            var border = UIKit.Stretch(panel, "Glow");
            border.offsetMin = new Vector2(-14f, -14f);
            border.offsetMax = new Vector2(14f, 14f);
            UIKit.Image(border, UIKit.SoftGlow, new Color(1f, 0.78f, 0.1f, 0.35f));
            border.SetAsFirstSibling();

            var icon = UIKit.Rect(panel, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(100f, 100f));
            UIKit.Image(icon, UIKit.Circle, new Color(1f, 0.78f, 0.1f, 0.2f));
            UIKit.TrophyIcon(icon, UIKit.Accent);

            UIKit.Label(panel, "ACHIEVEMENT UNLOCKED", 26, UIKit.Accent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(150f, 32f), new Vector2(500f, 36f), TextAnchor.MiddleLeft, true);
            UIKit.Label(panel, a.Name, 46, UIKit.TextMain, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(150f, -12f), new Vector2(520f, 56f), TextAnchor.MiddleLeft, true);
            UIKit.Label(panel, a.Description, 22, UIKit.TextDim, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(150f, -48f), new Vector2(520f, 30f), TextAnchor.MiddleLeft);
            if (a.Reward > 0)
                UIKit.Label(panel, "+" + UIKit.Money(a.Reward), 40, UIKit.Good, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-30f, 0f), new Vector2(200f, 60f), TextAnchor.MiddleRight, true);

            ModelLibrary.PlayAt("impact", Vector3.zero, 0.5f, 1.9f, 0f);
            current = panel;
            shownAt = Time.unscaledTime;
        }
    }
}
