using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrJones
{
    /// <summary>
    /// Remembers explored cells (faint glow on the map), draws the minimap, the compass arrow
    /// and edge warnings for rolling boulders that are off screen.
    /// </summary>
    public sealed class ExplorationHud : MonoBehaviour
    {
        [Header("Память пройденного")]
        public float RevealRadius = 4.5f;
        public Color MemoryFloor = new Color(0.07f, 0.065f, 0.055f);
        public Color MemoryWall = new Color(0.05f, 0.055f, 0.045f);

        [Header("Мини-карта")]
        public float PixelsPerCell = 5f;

        [Header("Предупреждения")]
        public float BoulderWarnDistance = 13f;
        public int MaxBoulderArrows = 5;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        GridMap map;
        bool[,] explored;
        Texture2D texture;
        Color32[] pixels;
        RawImage minimap;
        RectTransform canvasRect;
        Image compass;
        readonly List<Image> warnings = new List<Image>();
        Sprite arrowSprite;
        MaterialPropertyBlock block;
        float tick;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            arrowSprite = MakeArrowSprite();
            BuildUi();
        }

        void LateUpdate()
        {
            var game = GameManager.Instance;
            if (game == null || game.Map == null || game.Built == null || game.Player == null)
            {
                SetVisible(false);
                return;
            }

            if (game.Map != map)
                Reset(game.Map);
            bool playing = Time.timeScale > 0f;
            SetVisible(playing);
            if (!playing)
                return;

            tick -= Time.deltaTime;
            if (tick <= 0f)
            {
                tick = 0.1f;
                Reveal(game);
                DrawMinimap(game);
            }

            if (compass != null)
                compass.enabled = false;
            UpdateWarnings(game);
        }

        void Reset(GridMap next)
        {
            map = next;
            explored = new bool[map.Width, map.Height];
            texture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[map.Width * map.Height];
            minimap.texture = texture;
            minimap.rectTransform.sizeDelta = new Vector2(map.Width * PixelsPerCell, map.Height * PixelsPerCell);
            tick = 0f;
        }

        void Reveal(GameManager game)
        {
            Vector3 p = game.Player.transform.position;
            var center = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
            int r = Mathf.CeilToInt(RevealRadius);
            for (int y = center.y - r; y <= center.y + r; y++)
            {
                for (int x = center.x - r; x <= center.x + r; x++)
                {
                    if (x < 0 || y < 0 || x >= map.Width || y >= map.Height || explored[x, y])
                        continue;
                    float dx = x - p.x;
                    float dz = y - p.z;
                    if (dx * dx + dz * dz > RevealRadius * RevealRadius)
                        continue;
                    if (!LineOfSight(p.x, p.z, x, y))
                        continue;
                    explored[x, y] = true;
                    var renderer = game.Built.CellRenderers[x, y];
                    if (renderer == null)
                        continue;
                    if (map.Cells[x, y] == CellKind.Wall)
                        continue;
                    renderer.GetPropertyBlock(block);
                    block.SetColor(EmissionColorId, MemoryFloor);
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        bool LineOfSight(float fromX, float fromZ, int toX, int toY)
        {
            float dx = toX - fromX;
            float dz = toY - fromZ;
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) * 3f);
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                int cx = Mathf.RoundToInt(fromX + dx * t);
                int cy = Mathf.RoundToInt(fromZ + dz * t);
                if (cx == toX && cy == toY)
                    return true;
                if (map.Cells[cx, cy] != CellKind.Floor)
                    return false;
            }

            return true;
        }

        void DrawMinimap(GameManager game)
        {
            var hidden = new Color32(0, 0, 0, 70);
            var floor = new Color32(150, 135, 105, 235);
            var wall = new Color32(42, 52, 36, 235);
            var breakable = new Color32(170, 90, 40, 235);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Color32 c = hidden;
                    if (explored[x, y])
                    {
                        var kind = map.Cells[x, y];
                        c = kind == CellKind.Floor ? floor : kind == CellKind.Wall ? wall : breakable;
                    }

                    pixels[y * map.Width + x] = c;
                }
            }

            var built = game.Built;
            var itemColor = map.Level == LevelId.BoulderTemple ? new Color32(255, 205, 50, 255) : new Color32(90, 220, 255, 255);
            if (built.Items != null)
            {
                for (int i = 0; i < built.Items.Length; i++)
                {
                    if (built.Items[i] != null)
                        Mark(built.Items[i].transform.position, itemColor, true);
                }
            }

            if (built.Hearts != null)
            {
                for (int i = 0; i < built.Hearts.Length; i++)
                {
                    if (built.Hearts[i] != null)
                        Mark(built.Hearts[i].transform.position, new Color32(240, 60, 70, 255), true);
                }
            }

            if (built.Boulders != null)
            {
                Vector3 player = game.Player.transform.position;
                for (int i = 0; i < built.Boulders.Length; i++)
                {
                    var boulder = built.Boulders[i];
                    if (boulder == null || !boulder.IsRolling)
                        continue;
                    if ((boulder.transform.position - player).sqrMagnitude < BoulderWarnDistance * BoulderWarnDistance)
                        Mark(boulder.transform.position, new Color32(255, 120, 40, 255), false);
                }
            }

            if (game.ExitRevealed)
                Mark(GridMap.World(map.ExitCell, 0f), new Color32(80, 255, 120, 255), false);
            bool blink = Mathf.Repeat(Time.time, 0.6f) < 0.4f;
            Mark(game.Player.transform.position, blink ? new Color32(255, 255, 255, 255) : new Color32(255, 60, 40, 255), false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        void Mark(Vector3 world, Color32 color, bool onlyIfExplored)
        {
            int x = Mathf.RoundToInt(world.x);
            int y = Mathf.RoundToInt(world.z);
            if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
                return;
            if (onlyIfExplored && !explored[x, y])
                return;
            pixels[y * map.Width + x] = color;
        }

        void UpdateCompass(GameManager game)
        {
            Vector3 player = game.Player.transform.position;
            Vector3 target = Vector3.zero;
            bool found = false;
            Color color = Color.white;
            if (game.ExitRevealed)
            {
                target = GridMap.World(map.ExitCell, 0f);
                color = new Color(0.4f, 1f, 0.5f);
                found = true;
            }
            else if (game.Built.Items != null)
            {
                float best = float.MaxValue;
                for (int i = 0; i < game.Built.Items.Length; i++)
                {
                    var item = game.Built.Items[i];
                    if (item == null)
                        continue;
                    float d = (item.transform.position - player).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        target = item.transform.position;
                        found = true;
                    }
                }

                color = map.Level == LevelId.BoulderTemple ? new Color(1f, 0.82f, 0.25f) : new Color(0.45f, 0.88f, 1f);
            }

            if (!found || !PlaceAtEdge(compass, target, 46f))
            {
                compass.enabled = false;
                return;
            }

            compass.enabled = true;
            float pulse = 0.75f + Mathf.Sin(Time.time * 4f) * 0.25f;
            compass.color = new Color(color.r, color.g, color.b, pulse);
        }

        void UpdateWarnings(GameManager game)
        {
            int used = 0;
            var boulders = game.Built.Boulders;
            Vector3 player = game.Player.transform.position;
            if (boulders != null)
            {
                for (int i = 0; i < boulders.Length && used < warnings.Count; i++)
                {
                    var boulder = boulders[i];
                    if (boulder == null || !boulder.IsRolling)
                        continue;
                    Vector3 offset = boulder.transform.position - player;
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    if (distance > BoulderWarnDistance)
                        continue;
                    var arrow = warnings[used];
                    if (!PlaceAtEdge(arrow, boulder.transform.position, 20f))
                        continue;
                    float closeness = 1f - distance / BoulderWarnDistance;
                    float pulse = 0.6f + Mathf.Sin(Time.time * (6f + closeness * 10f)) * 0.4f;
                    arrow.enabled = true;
                    arrow.color = new Color(1f, 0.38f, 0.15f, Mathf.Clamp01(0.25f + closeness) * pulse);
                    arrow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.25f, closeness);
                    used++;
                }
            }

            for (int i = used; i < warnings.Count; i++)
                warnings[i].enabled = false;
        }

        bool PlaceAtEdge(Image arrow, Vector3 world, float inset)
        {
            var cam = Camera.main;
            if (cam == null || canvasRect == null)
                return false;
            Vector3 viewport = cam.WorldToViewportPoint(world);
            if (viewport.x > 0.04f && viewport.x < 0.96f && viewport.y > 0.04f && viewport.y < 0.96f)
                return false;

            Vector2 size = canvasRect.rect.size;
            Vector2 dir = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            dir.x *= size.x;
            dir.y *= size.y;
            if (dir.sqrMagnitude < 0.0001f)
                return false;
            Vector2 half = size * 0.5f - new Vector2(inset + 30f, inset + 30f);
            float scale = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(dir.x)), half.y / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
            arrow.rectTransform.anchoredPosition = dir * scale;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            return true;
        }

        void SetVisible(bool visible)
        {
            if (minimap != null && minimap.transform.parent.gameObject.activeSelf != visible)
                minimap.transform.parent.gameObject.SetActive(visible);
            if (!visible)
            {
                if (compass != null)
                    compass.enabled = false;
                for (int i = 0; i < warnings.Count; i++)
                    warnings[i].enabled = false;
            }
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("ExplorationCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasRect = canvasGo.GetComponent<RectTransform>();

            var frame = new GameObject("Minimap");
            frame.transform.SetParent(canvasGo.transform, false);
            var frameRect = frame.AddComponent<RectTransform>();
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(1f, 1f);
            frameRect.pivot = new Vector2(1f, 1f);
            frameRect.anchoredPosition = new Vector2(-20f, -78f);
            frameRect.sizeDelta = new Vector2(10f, 10f);
            var mapGo = new GameObject("Map");
            mapGo.transform.SetParent(frame.transform, false);
            minimap = mapGo.AddComponent<RawImage>();
            minimap.raycastTarget = false;
            var mapRect = minimap.rectTransform;
            mapRect.anchorMin = mapRect.anchorMax = new Vector2(1f, 1f);
            mapRect.pivot = new Vector2(1f, 1f);
            mapRect.anchoredPosition = Vector2.zero;

            compass = MakeArrow(canvasGo.transform, "Compass", 54f);
            for (int i = 0; i < MaxBoulderArrows; i++)
                warnings.Add(MakeArrow(canvasGo.transform, "BoulderWarning", 40f));
        }

        Image MakeArrow(Transform parent, string name, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.sprite = arrowSprite;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        static Sprite MakeArrowSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var data = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)(n - 1);
                    float v = Mathf.Abs(y / (float)(n - 1) - 0.5f) * 2f;
                    bool inside = u > 0.12f && v < (1f - u) * 1.05f;
                    bool rim = inside && v > (1f - u) * 1.05f - 0.12f;
                    byte shade = (byte)(rim ? 150 : 255);
                    data[y * n + x] = inside ? new Color32(shade, shade, shade, 255) : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(data);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
