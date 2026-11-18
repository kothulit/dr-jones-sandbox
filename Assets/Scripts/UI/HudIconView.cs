using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DrJones
{
    public sealed class HudIconView : MonoBehaviour
    {
        public enum Kind
        {
            Heart,
            Idol,
            Hammer
        }

        RawImage image;
        RenderTexture rt;
        Camera cam;
        Transform pivot;
        GameObject stage;
        GameObject model;
        Kind kind;
        bool filled = true;
        float pulse;
        Vector3 modelBaseScale = Vector3.one;
        static int nextSlot;

        public static HudIconView Create(Transform parent, Kind kind, Vector2 size)
        {
            var go = new GameObject(kind + "Icon");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;
            layout.minWidth = size.x;
            layout.minHeight = size.y;
            var view = go.AddComponent<HudIconView>();
            view.Build(kind);
            return view;
        }

        public void SetKind(Kind value)
        {
            if (kind == value && model != null)
                return;
            SpawnModel(value);
        }

        public void SetFilled(bool value)
        {
            filled = value;
            if (image != null)
                image.color = value ? Color.white : new Color(0.22f, 0.16f, 0.16f, 0.45f);
        }

        void Build(Kind value)
        {
            image = gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            rt = new RenderTexture(160, 160, 16, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "HudIcon"
            };
            rt.Create();
            image.texture = rt;
            image.color = Color.white;

            int slot = nextSlot++;
            stage = new GameObject("HudStage");
            stage.transform.position = new Vector3(9000f + slot * 8f, 120f, 9000f);

            var lightGo = new GameObject("Key");
            lightGo.transform.SetParent(stage.transform, false);
            lightGo.transform.localPosition = new Vector3(0.7f, 1.15f, -0.7f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.94f, 0.86f);
            light.range = 5f;
            light.intensity = 4.2f;
            light.shadows = LightShadows.None;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(stage.transform, false);
            fillGo.transform.localPosition = new Vector3(-0.65f, 0.35f, 0.55f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.55f, 0.62f, 0.7f);
            fill.range = 4.5f;
            fill.intensity = 1.4f;
            fill.shadows = LightShadows.None;

            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(stage.transform, false);

            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(stage.transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 8f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.cullingMask = ~0;
            cam.targetTexture = rt;
            cam.depth = -20;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;

            SpawnModel(value);
        }

        void SpawnModel(Kind value)
        {
            kind = value;
            if (model != null)
                Destroy(model);
            switch (value)
            {
                case Kind.Idol:
                    model = VisualFactory.Idol();
                    break;
                case Kind.Hammer:
                    model = VisualFactory.Hammer();
                    break;
                default:
                    model = VisualFactory.Heart();
                    break;
            }

            VisualFactory.PrepareHudModel(model);
            model.transform.SetParent(pivot, false);
            modelBaseScale = Vector3.one;
            pulse = Random.value * 6f;
            if (value == Kind.Heart)
            {
                model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                model.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            }
            else
            {
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }

            FrameCamera();
            SetFilled(filled);
        }

        void FrameCamera()
        {
            if (cam == null)
                return;
            switch (kind)
            {
                case Kind.Hammer:
                    cam.transform.localPosition = new Vector3(0.55f, 0.62f, -1.15f);
                    cam.orthographicSize = 0.52f;
                    break;
                case Kind.Idol:
                    cam.transform.localPosition = new Vector3(0.5f, 0.48f, -1.05f);
                    cam.orthographicSize = 0.42f;
                    break;
                default:
                    cam.transform.localPosition = new Vector3(0f, 0.28f, -0.95f);
                    cam.orthographicSize = 0.36f;
                    break;
            }

            Vector3 look = kind == Kind.Heart
                ? pivot.position + Vector3.up * 0.22f
                : pivot.position + Vector3.up * 0.34f;
            cam.transform.LookAt(look);
        }

        void LateUpdate()
        {
            if (kind == Kind.Heart)
            {
                pulse += Time.unscaledDeltaTime;
                float scale = 1f + Mathf.Sin(pulse * 4f) * 0.08f;
                if (model != null)
                    model.transform.localScale = modelBaseScale * scale;
                return;
            }

            if (pivot != null)
                pivot.Rotate(0f, 40f * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        void OnEnable()
        {
            if (stage != null)
                stage.SetActive(true);
            if (cam != null)
                cam.enabled = true;
        }

        void OnDisable()
        {
            if (cam != null)
                cam.enabled = false;
            if (stage != null)
                stage.SetActive(false);
        }

        void OnDestroy()
        {
            if (cam != null && cam.targetTexture != null)
                cam.targetTexture = null;
            if (stage != null)
                Destroy(stage);
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
        }
    }
}
