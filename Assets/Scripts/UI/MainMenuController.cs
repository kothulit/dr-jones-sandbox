using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DrJones
{
    public sealed class MainMenuController : MonoBehaviour
    {
        Font font;
        Transform exhibit;

        void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (GetComponent<AudioManager>() == null)
                gameObject.AddComponent<AudioManager>();
            font = GameFonts.Ui;
            BuildScene();
            BuildUi();
        }

        void Start()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMenu();
        }

        void Update()
        {
            if (exhibit != null)
                exhibit.Rotate(0f, 12f * Time.deltaTime, 0f, Space.World);
        }

        void BuildScene()
        {
            if (Camera.main == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                var camera = cameraGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.02f, 0.015f, 0.02f);
                camera.fieldOfView = 32f;
                camera.transform.position = new Vector3(0f, 1.85f, -4.2f);
                camera.transform.LookAt(new Vector3(0f, 0.22f, 0f));
                cameraGo.AddComponent<AudioListener>();
            }
            else
            {
                var camera = Camera.main;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.02f, 0.015f, 0.02f);
                camera.orthographic = false;
                camera.fieldOfView = 32f;
                camera.transform.position = new Vector3(0f, 1.85f, -4.2f);
                camera.transform.LookAt(new Vector3(0f, 0.22f, 0f));
                if (camera.GetComponent<AudioListener>() == null)
                    camera.gameObject.AddComponent<AudioListener>();
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.035f, 0.04f);

            var key = new GameObject("MenuLight");
            var light = key.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.55f);
            light.range = 10f;
            light.intensity = 18f;
            key.transform.position = new Vector3(0.4f, 2.4f, -2.2f);

            exhibit = new GameObject("Exhibit").transform;
            exhibit.position = new Vector3(0f, 0.15f, 0f);
            var hat = VisualFactory.Hat();
            hat.transform.SetParent(exhibit, false);
            hat.transform.localPosition = Vector3.zero;
            hat.transform.localScale = Vector3.one * 2.4f;
            hat.transform.localRotation = Quaternion.Euler(-18f, 18f, 8f);
            var idol = VisualFactory.Idol();
            idol.transform.SetParent(exhibit, false);
            idol.transform.localPosition = new Vector3(-2.1f, -0.15f, 0.55f);
            idol.transform.localScale = Vector3.one * 0.7f;
            var hammer = VisualFactory.Hammer();
            hammer.transform.SetParent(exhibit, false);
            hammer.transform.localPosition = new Vector3(2.15f, -0.2f, 0.55f);
            hammer.transform.localScale = Vector3.one * 0.7f;
            foreach (var extra in exhibit.GetComponentsInChildren<Light>())
                extra.enabled = false;
            foreach (var extra in hat.GetComponentsInChildren<ParticleSystem>())
                extra.gameObject.SetActive(false);
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("MenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            }

            TextBlock("Dr. Jones", 92, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 110f), new Color(1f, 0.86f, 0.45f), FontStyle.Bold, GameFonts.Title);
            TextBlock("Исследуй древние храмы, собирай сокровища и избегай опасных ловушек!\nНайди все предметы и выберись из храма живым!",
                28, new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(1800f, 90f), Color.white, FontStyle.Normal);
            TextBlock("Стрелки или WASD — движение\nПробел — пауза\nEsc — выход в меню",
                26, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(640f, 110f), new Color(0.9f, 0.85f, 0.7f), FontStyle.Normal);

            TextBlock(BestLine(), 26, new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(1200f, 56f), new Color(0.9f, 0.85f, 0.7f), FontStyle.Normal);
            if (Resources.Load<TextAsset>("Credits") != null)
                TextBlock("Музыка, звуки и текстуры: авторы указаны в Credits.txt", 18, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(1400f, 28f), new Color(1f, 1f, 1f, 0.45f), FontStyle.Normal);

            TextBlock("НАЧАТЬ ИГРУ", 46, new Vector2(0.5f, 0.5f), new Vector2(0f, 128f), new Vector2(780f, 64f), new Color(1f, 0.86f, 0.45f), FontStyle.Bold);
            TextBlock("уровень сложности", 24, new Vector2(0.5f, 0.5f), new Vector2(0f, 74f), new Vector2(700f, 36f), new Color(0.9f, 0.85f, 0.7f), FontStyle.Normal);
            MakeButton("Расхититель", new Vector2(0f, 8f), () => StartRun(Difficulty.Raider));
            MakeButton("Профессионал", new Vector2(0f, -82f), () => StartRun(Difficulty.Professional));
            MakeButton("Новичок", new Vector2(0f, -172f), () => StartRun(Difficulty.Novice));
            MakeButton("Выход", new Vector2(0f, -262f), Quit);
        }

        void TextBlock(string value, int size, Vector2 anchor, Vector2 pos, Vector2 box, Color color, FontStyle style, Font overrideFont = null)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(FindAnyObjectByType<Canvas>().transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y >= 0.99f ? 1f : 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = box;
            var text = go.AddComponent<Text>();
            text.font = overrideFont != null ? overrideFont : font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void MakeButton(string caption, Vector2 pos, UnityEngine.Events.UnityAction click)
        {
            var go = new GameObject(caption);
            go.transform.SetParent(FindAnyObjectByType<Canvas>().transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 78f);
            rect.anchoredPosition = pos;
            var image = go.AddComponent<Image>();
            image.color = new Color(0.28f, 0.19f, 0.1f, 0.94f);
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.55f, 0.4f, 0.18f);
            colors.pressedColor = new Color(0.75f, 0.58f, 0.25f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayUi();
            });
            button.onClick.AddListener(click);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.9f, 0.65f);
            label.text = caption;
            label.raycastTarget = false;
        }

        static void StartRun(Difficulty difficulty)
        {
            GameSettings.Current = difficulty;
            SceneManager.LoadScene(GameManager.GameScene);
        }

        static string BestLine()
        {
            float best = 0f;
            foreach (Difficulty difficulty in System.Enum.GetValues(typeof(Difficulty)))
            {
                float time = PlayerPrefs.GetFloat(GameSettings.BestTimeKey(difficulty), 0f);
                if (time > 0f && (best <= 0f || time < best))
                    best = time;
            }

            return best <= 0f ? "" : "Лучшее время — " + UIManager.Format(best);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
