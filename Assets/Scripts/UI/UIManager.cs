using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DrJones
{
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        Font font;
        HudIconView[] hearts;
        HudIconView objectiveIcon;
        Text objectivePrefix;
        Text objective;
        Text clock;
        Text seed;
        GameObject escapeHud;
        Text escapeCount;
        Text escapeHint;
        GameObject hud;
        GameObject pausePanel;
        GameObject endPanel;
        Text endTitle;
        Text endBody;
        Image fade;
        Text retryLabel;
        Action retryClick;
        int shownHealth = -1;
        LevelId shownLevel = (LevelId)(-1);

        void Awake()
        {
            Instance = this;
            font = GameFonts.Ui;
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (game == null || hud == null || !hud.activeSelf)
                return;
            int hp = game.HealthCurrent;
            if (hp != shownHealth)
            {
                shownHealth = hp;
                for (int i = 0; i < hearts.Length; i++)
                    hearts[i].SetFilled(i < hp);
            }

            var level = game.Map != null ? game.Map.Level : LevelId.BoulderTemple;
            if (level != shownLevel)
            {
                shownLevel = level;
                bool idols = level == LevelId.BoulderTemple;
                objectiveIcon.SetKind(idols ? HudIconView.Kind.Idol : HudIconView.Kind.Hammer);
                if (objectivePrefix != null)
                    objectivePrefix.text = idols ? "Золотые идолы" : "Осколки молота";
            }

            objective.text = game.ObjectiveText;
            float left = game.Timer != null ? game.Timer.Remaining : 0f;
            clock.text = "Время: " + Format(left);
            clock.color = left < 10f ? new Color(1f, 0.45f, 0.35f) : Color.white;
        }

        public void ShowPause(bool paused)
        {
            if (pausePanel != null)
                pausePanel.SetActive(paused);
        }

        public void ShowHud(bool visible)
        {
            if (hud != null)
                hud.SetActive(visible);
        }

        public void SetSeed(int value)
        {
            if (seed != null)
                seed.text = value.ToString();
        }

        public void ShowVictory(float totalSeconds, float bestSeconds, bool newRecord, bool cheated = false)
        {
            var game = GameManager.Instance;
            string mode = game != null ? GameSettings.Title(game.Difficulty) : "";
            string body = cheated
                ? "Храмы пройдены (" + mode + ").\nФууу! Читер!"
                : "Храмы пройдены (" + mode + ").\nСумма двух уровней: " + Format(totalSeconds) + "\n" + (newRecord ? "Новый рекорд!" : "Рекорд: " + Format(bestSeconds));
            ShowEnd("ПОБЕДА", body, "Играть снова", false);
        }

        public void ShowDefeat(FailReason reason, bool canContinue = false)
        {
            string body = reason == FailReason.Time
                ? "Время на этот уровень вышло."
                : reason == FailReason.Sealed
                    ? "Плита закрыла выход."
                    : "Здоровье кончилось.";
            ShowEnd("ПОРАЖЕНИЕ", body, canContinue ? "Продолжить" : "Попробовать снова", true, canContinue);
        }

        public void SetEscapeCountdown(bool visible, float remaining = 0f)
        {
            if (escapeHud == null)
                return;
            escapeHud.SetActive(visible);
            if (!visible)
                return;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            escapeCount.text = seconds.ToString();
            float pulse = remaining <= 4f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)) : 1f;
            if (remaining <= 4f)
                escapeCount.color = new Color(1f, 0.32f, 0.22f) * pulse + new Color(0.4f, 0f, 0f, 0f);
            else if (remaining <= 8f)
                escapeCount.color = new Color(1f, 0.62f, 0.28f);
            else
                escapeCount.color = new Color(1f, 0.9f, 0.55f);
        }

        public void SetFade(float alpha)
        {
            if (fade == null)
                return;
            var color = fade.color;
            color.a = alpha;
            fade.color = color;
            fade.raycastTarget = alpha > 0.01f;
        }

        public IEnumerator FadeTo(float alpha, float duration)
        {
            float from = fade != null ? fade.color.a : 0f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetFade(Mathf.Lerp(from, alpha, Mathf.Clamp01(t / duration)));
                yield return null;
            }

            SetFade(alpha);
        }

        void ShowEnd(string title, string body, string retry, bool defeat, bool continueLevel = false)
        {
            ShowHud(false);
            ShowPause(false);
            endPanel.SetActive(true);
            endTitle.text = title;
            endTitle.color = defeat ? new Color(0.9f, 0.35f, 0.28f) : new Color(1f, 0.86f, 0.45f);
            endBody.text = body;
            if (retryLabel != null)
                retryLabel.text = retry;
            retryClick = () =>
            {
                var game = GameManager.Instance;
                if (game == null)
                    return;
                if (continueLevel)
                    game.ContinueLevel();
                else
                    game.Restart();
            };
        }

        void Build()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            hud = new GameObject("Hud");
            hud.transform.SetParent(canvasGo.transform, false);
            Stretch(hud.AddComponent<RectTransform>());
            var heartRow = Row(hud.transform, "Hearts", new Vector2(18f, -12f), new Vector2(0f, 1f), new Vector2(0f, 1f), 8f);
            hearts = new HudIconView[3];
            for (int i = 0; i < 3; i++)
                hearts[i] = HudIconView.Create(heartRow, HudIconView.Kind.Heart, new Vector2(72f, 72f));

            var objectiveRow = Row(hud.transform, "Objective", new Vector2(0f, -8f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 6f);
            objectiveRow.pivot = new Vector2(0.5f, 1f);
            var fitter = objectiveRow.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            objectivePrefix = RowLabel(objectiveRow, "Золотые идолы", 30, 280f, TextAnchor.MiddleRight);
            objectiveIcon = HudIconView.Create(objectiveRow, HudIconView.Kind.Idol, new Vector2(72f, 72f));
            objective = RowLabel(objectiveRow, "0/5", 34, 220f, TextAnchor.MiddleLeft);
            clock = Label(hud.transform, "Время: 1:00", 32, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(320f, 50f));
            seed = Label(hud.transform, "", 16, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(16f, 12f), new Vector2(200f, 28f));
            seed.color = new Color(1f, 1f, 1f, 0.35f);

            escapeHud = new GameObject("Escape");
            escapeHud.transform.SetParent(hud.transform, false);
            Stretch(escapeHud.AddComponent<RectTransform>());
            escapeHint = Label(escapeHud.transform, "ВЫХОД ЗАКРЫВАЕТСЯ", 28, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(900f, 40f));
            escapeHint.color = new Color(1f, 0.86f, 0.5f);
            escapeCount = Label(escapeHud.transform, "12", 92, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(400f, 110f));
            escapeCount.font = GameFonts.Title;
            escapeCount.color = new Color(1f, 0.9f, 0.55f);
            escapeHud.SetActive(false);

            pausePanel = Panel(canvasGo.transform, new Color(0f, 0f, 0f, 0.35f));
            var pauseLabel = Label(pausePanel.transform, "ПАУЗА", 92, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(800f, 140f));
            pauseLabel.color = new Color(1f, 0.9f, 0.55f);
            var pauseHint = Label(pausePanel.transform, "Пробел — продолжить    Esc — выход в меню", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(900f, 40f));
            pauseHint.color = new Color(0.9f, 0.85f, 0.7f);
            pausePanel.SetActive(false);

            endPanel = Panel(canvasGo.transform, new Color(0.02f, 0.02f, 0.03f, 0.82f));
            endTitle = Label(endPanel.transform, "ПОБЕДА", 84, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1000f, 120f));
            endBody = Label(endPanel.transform, "", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(900f, 150f));
            retryLabel = Button(endPanel.transform, "Retry", "Играть снова", new Vector2(0f, -90f), () =>
            {
                retryClick?.Invoke();
            });
            retryClick = () =>
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.Restart();
            };
            Button(endPanel.transform, "Exit", "Выход", new Vector2(0f, -170f), () =>
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.ExitToMenu();
            });
            endPanel.SetActive(false);

            var fadeGo = new GameObject("Fade");
            fadeGo.transform.SetParent(canvasGo.transform, false);
            var fadeRect = fadeGo.AddComponent<RectTransform>();
            Stretch(fadeRect);
            fade = fadeGo.AddComponent<Image>();
            fade.color = new Color(0f, 0f, 0f, 0f);
            fade.raycastTarget = false;
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        RectTransform Row(Transform parent, string name, Vector2 anchored, Vector2 anchorMin, Vector2 anchorMax, float spacing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x < 0.1f ? 0f : 0.5f, 1f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = new Vector2(360f, 76f);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        GameObject Panel(Transform parent, Color color)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            Stretch(go.AddComponent<RectTransform>());
            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        Text RowLabel(Transform parent, string value, int size, float width, TextAnchor align)
        {
            var text = Label(parent, value, size, align, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 56f));
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = 56f;
            return text;
        }

        Text Label(Transform parent, string value, int size, TextAnchor anchor, Vector2 screenAnchor, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = screenAnchor;
            rect.anchorMax = screenAnchor;
            float pivotX = 0.5f;
            float pivotY = 0.5f;
            if (screenAnchor.x <= 0.01f)
                pivotX = 0f;
            if (screenAnchor.x >= 0.99f)
                pivotX = 1f;
            if (screenAnchor.y >= 0.99f)
                pivotY = 1f;
            if (screenAnchor.y <= 0.01f)
                pivotY = 0f;
            rect.pivot = new Vector2(pivotX, pivotY);
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        Text Button(Transform parent, string name, string caption, Vector2 pos, Action click)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 64f);
            rect.anchoredPosition = pos;
            var image = go.AddComponent<Image>();
            image.color = new Color(0.28f, 0.2f, 0.12f, 0.95f);
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.55f, 0.4f, 0.18f);
            colors.pressedColor = new Color(0.7f, 0.55f, 0.25f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayUi();
                click();
            });
            var text = Label(go.transform, caption, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 60f));
            text.color = new Color(1f, 0.9f, 0.65f);
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static string Format(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds + 0.001f));
            int min = total / 60;
            int sec = total % 60;
            return min + ":" + sec.ToString("00");
        }
    }
}
