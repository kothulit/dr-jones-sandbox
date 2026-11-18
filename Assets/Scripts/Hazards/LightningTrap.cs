using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DrJones
{
    public sealed class LightningTrap : MonoBehaviour
    {
        public float FirstDelay = 2f;
        public float Interval = 2f;
        public float WarningTime = 0.4f;
        public float StrikeTime = 0.16f;
        public float HitRadius = 0.34f;

        const float CorridorHalf = 0.52f;
        static readonly Color CoreBright = Color.white;
        static readonly Color EdgeBright = new Color(1f, 1f, 1f, 0.85f);
        static readonly Color HintColor = new Color(1f, 1f, 1f, 0.22f);

        enum Phase
        {
            Wait,
            Warning,
            Strike,
            Gap
        }

        Vector3 origin;
        Vector3 end;
        Vector3 axisFrom;
        Vector3 axisTo;
        Vector3 axisDir;
        Vector3 axisSide;
        float axisLength;
        Phase phase = Phase.Wait;
        float timer;
        float flicker;
        LineRenderer hint;
        LineRenderer glowLine;
        LineRenderer coreLine;
        readonly List<LineRenderer> forks = new List<LineRenderer>();
        Light[] lamps;
        List<Vector3> mainPath = new List<Vector3>();
        bool damagedThisStrike;
        float boltGlow;

        public void Setup(Vector3 from, Vector3 to, float firstDelay, float interval, float warning)
        {
            origin = from;
            end = to;
            FirstDelay = firstDelay;
            Interval = interval;
            WarningTime = warning;
            transform.position = from;
            axisFrom = origin + Vector3.up * 0.48f;
            axisTo = end + Vector3.up * 0.48f;
            Vector3 delta = axisTo - axisFrom;
            axisLength = Mathf.Max(0.01f, delta.magnitude);
            axisDir = delta / axisLength;
            axisSide = Vector3.Cross(axisDir, Vector3.up);
            if (axisSide.sqrMagnitude < 0.0001f)
                axisSide = Vector3.Cross(axisDir, Vector3.right);
            axisSide.Normalize();

            hint = MakeLine("Hint", 0.04f, HintColor, false);
            glowLine = MakeLine("Glow", 0.14f, EdgeBright, true);
            coreLine = MakeLine("Core", 0.04f, CoreBright, true);
            for (int i = 0; i < 3; i++)
            {
                var fork = MakeLine("Fork" + i, 0.035f, EdgeBright, true);
                fork.enabled = false;
                forks.Add(fork);
            }

            int lampsCount = Mathf.Max(2, Mathf.RoundToInt(axisLength) + 1);
            float spacing = axisLength / (lampsCount - 1);
            lamps = new Light[lampsCount];
            for (int i = 0; i < lampsCount; i++)
            {
                var go = new GameObject("Lamp");
                go.transform.SetParent(transform, false);
                go.transform.position = Vector3.Lerp(axisFrom, axisTo, i / (float)(lampsCount - 1));
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Color.white;
                light.range = spacing * 2.4f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                lamps[i] = light;
            }

            DrawHint();
            ShowIdle();
        }

        LineRenderer MakeLine(string name, float width, Color color, bool emissive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            line.numCornerVertices = 0;
            line.alignment = LineAlignment.View;
            line.startWidth = width;
            line.endWidth = width;
            line.material = MakeLineMaterial(emissive);
            Paint(line, color, emissive ? 1f : 0f);
            line.enabled = false;
            return line;
        }

        static Material MakeLineMaterial(bool emissive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", emissive ? 2f : 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", emissive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            material.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.white);
            }

            return material;
        }

        static void Paint(LineRenderer line, Color color, float emission = 0f)
        {
            if (line == null)
                return;
            line.startColor = color;
            line.endColor = color;
            var material = line.material;
            if (material == null)
                return;
            var hdr = color;
            if (emission > 1f)
            {
                hdr.r *= emission;
                hdr.g *= emission;
                hdr.b *= emission;
            }

            material.color = hdr;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", hdr);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * emission);
        }

        static void Width(LineRenderer line, float start, float end)
        {
            line.startWidth = start;
            line.endWidth = end;
        }

        void DrawHint()
        {
            hint.positionCount = 2;
            hint.SetPosition(0, axisFrom);
            hint.SetPosition(1, axisTo);
            hint.enabled = true;
            Width(hint, 0.028f, 0.018f);
            Paint(hint, HintColor, 0f);
        }

        void Update()
        {
            if (Time.timeScale <= 0f || (GameManager.Instance != null && GameManager.Instance.SimulationFrozen))
                return;
            timer += Time.deltaTime;
            switch (phase)
            {
                case Phase.Wait:
                    if (timer >= Mathf.Max(0.05f, FirstDelay - WarningTime))
                        BeginWarning();
                    break;
                case Phase.Warning:
                    TickFlicker(0.07f, 0.16f, 4, false);
                    SetLamps(3.8f);
                    if (timer >= WarningTime)
                        BeginStrike();
                    break;
                case Phase.Strike:
                    TickFlicker(0.025f, 0.48f, 5, true);
                    if (!damagedThisStrike)
                        TryHit();
                    SetLamps(Mathf.Lerp(18f, 3f, timer / StrikeTime));
                    if (timer >= StrikeTime)
                    {
                        phase = Phase.Gap;
                        timer = 0f;
                        HideBolt();
                        ShowIdle();
                    }
                    break;
                case Phase.Gap:
                    if (timer >= Mathf.Max(0.05f, Interval - WarningTime))
                        BeginWarning();
                    break;
            }
        }

        void TickFlicker(float period, float displace, int generations, bool withForks)
        {
            flicker += Time.deltaTime;
            if (flicker < period)
                return;
            flicker = 0f;
            DrawBolt(displace, generations, withForks);
        }

        void ShowIdle()
        {
            HideBolt();
            hint.enabled = true;
            Paint(hint, HintColor, 0f);
            Width(hint, 0.028f, 0.018f);
            boltGlow = 0f;
            PlaceLampsOnAxis();
            SetLamps(0f);
        }

        void BeginWarning()
        {
            phase = Phase.Warning;
            timer = 0f;
            flicker = 0f;
            Paint(hint, HintColor, 0f);
            Width(hint, 0.03f, 0.02f);
            boltGlow = 2.2f;
            DrawBolt(0.28f, 4, false);
            Width(glowLine, 0.14f, 0.08f);
            Width(coreLine, 0.04f, 0.022f);
            SetLamps(3.8f);
        }

        void BeginStrike()
        {
            phase = Phase.Strike;
            timer = 0f;
            flicker = 0f;
            damagedThisStrike = false;
            boltGlow = 8f;
            DrawBolt(0.48f, 5, true);
            Width(glowLine, 0.22f, 0.12f);
            Width(coreLine, 0.07f, 0.035f);
            SetLamps(18f);
            var flash = Color.white;
            VisualFactory.Burst(origin + Vector3.up * 0.2f, flash, 10);
            VisualFactory.Burst(end + Vector3.up * 0.2f, flash, 10);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayLightning();
        }

        void DrawBolt(float displace, int generations, bool withForks)
        {
            mainPath = FractalBolt(axisFrom, axisTo, generations, displace);
            Apply(glowLine, mainPath);
            Apply(coreLine, mainPath);
            Paint(glowLine, EdgeBright, boltGlow * 0.55f);
            Paint(coreLine, CoreBright, boltGlow);
            glowLine.enabled = true;
            coreLine.enabled = true;

            HideForks();
            if (!withForks || mainPath.Count < 6)
                return;

            int forkCount = Random.Range(1, 3);
            for (int i = 0; i < forkCount; i++)
            {
                int start = Random.Range(2, mainPath.Count - 2);
                Vector3 a = mainPath[start];
                float along = Random.Range(0.15f, 0.45f);
                float side = Random.Range(-CorridorHalf, CorridorHalf);
                Vector3 b = ClampToCorridor(a + axisDir * along + axisSide * side);
                var forkPath = FractalBolt(a, b, 3, displace * 0.55f);
                Apply(forks[i], forkPath);
                Width(forks[i], 0.045f, 0.012f);
                Paint(forks[i], i == 0 ? EdgeBright : CoreBright, boltGlow * (i == 0 ? 0.45f : 0.8f));
                forks[i].enabled = true;
            }
        }

        List<Vector3> FractalBolt(Vector3 a, Vector3 b, int generations, float maxDisplace)
        {
            var points = new List<Vector3> { ClampToCorridor(a), ClampToCorridor(b) };
            float offset = Mathf.Min(maxDisplace, CorridorHalf);
            for (int g = 0; g < generations; g++)
            {
                var next = new List<Vector3>(points.Count * 2);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector3 p0 = points[i];
                    Vector3 p1 = points[i + 1];
                    Vector3 mid = (p0 + p1) * 0.5f;
                    Vector3 dir = p1 - p0;
                    float len = dir.magnitude;
                    if (len > 0.0001f)
                    {
                        dir /= len;
                        Vector3 perp = Vector3.Cross(dir, Vector3.up);
                        if (perp.sqrMagnitude < 0.0001f)
                            perp = Vector3.Cross(dir, Vector3.right);
                        perp.Normalize();
                        mid += perp * Random.Range(-offset, offset);
                        mid += Vector3.up * Random.Range(-0.06f, 0.06f);
                    }

                    next.Add(p0);
                    next.Add(ClampToCorridor(mid));
                }

                next.Add(points[points.Count - 1]);
                points = next;
                offset *= 0.5f;
            }

            return points;
        }

        Vector3 ClampToCorridor(Vector3 point)
        {
            Vector3 rel = point - axisFrom;
            float along = Mathf.Clamp(Vector3.Dot(rel, axisDir), 0f, axisLength);
            float side = Mathf.Clamp(Vector3.Dot(rel, axisSide), -CorridorHalf, CorridorHalf);
            Vector3 clamped = axisFrom + axisDir * along + axisSide * side;
            clamped.y = Mathf.Clamp(point.y, 0.28f, 0.72f);
            return clamped;
        }

        static void Apply(LineRenderer line, List<Vector3> points)
        {
            if (line == null || points == null || points.Count < 2)
                return;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
        }

        void HideBolt()
        {
            if (glowLine != null)
                glowLine.enabled = false;
            if (coreLine != null)
                coreLine.enabled = false;
            HideForks();
        }

        void HideForks()
        {
            for (int i = 0; i < forks.Count; i++)
            {
                if (forks[i] != null)
                    forks[i].enabled = false;
            }
        }

        void PlaceLampsOnAxis()
        {
            if (lamps == null)
                return;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null)
                    continue;
                float t = lamps.Length == 1 ? 0.5f : i / (float)(lamps.Length - 1);
                lamps[i].transform.position = Vector3.Lerp(axisFrom, axisTo, t);
            }
        }

        void SetLamps(float intensity)
        {
            if (lamps == null)
                return;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] != null)
                    lamps[i].intensity = intensity;
            }
        }

        void TryHit()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null)
                return;
            Vector3 p = player.Center;
            if (DistanceToSegment(p, origin, end) > HitRadius + player.HitRadius * 0.5f)
                return;
            damagedThisStrike = true;
            var health = player.GetComponent<HealthSystem>();
            var inv = player.GetComponent<InvulnerabilitySystem>();
            if (health != null && health.TryDamage(1, inv) && AudioManager.Instance != null)
                AudioManager.Instance.PlayHurt();
        }

        static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = 0f;
            a.y = 0f;
            b.y = 0f;
            Vector3 ab = b - a;
            float length = ab.sqrMagnitude;
            if (length < 0.0001f)
                return Vector3.Distance(point, a);
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / length);
            return Vector3.Distance(point, a + ab * t);
        }
    }
}
