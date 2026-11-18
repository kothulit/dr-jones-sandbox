using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DrJones
{
    public sealed class PlayerController : MonoBehaviour
    {
        public float Speed = 5.2f;
        public float Radius = 0.26f;
        public bool ControlEnabled = true;
        public bool HoldPose;
        public const float GroundY = 0.3f;

        SphereCollider body;
        float stepTimer;
        float horizontalPressedAt = -1f;
        float verticalPressedAt = -1f;

        void Awake()
        {
            body = GetComponent<SphereCollider>();
            if (body != null)
            {
                body.radius = Radius;
                body.center = new Vector3(0f, 0.05f, 0f);
            }
        }

        public void Configure(float speed, float radius, float torchRange, float torchIntensity)
        {
            Speed = speed;
            Radius = radius;
            if (body != null)
            {
                body.radius = radius;
                body.center = new Vector3(0f, 0.05f, 0f);
            }

            var flicker = GetComponentInChildren<TorchFlicker>();
            var torch = GetComponentInChildren<Light>();
            if (torch != null)
                ApplyTorch(torch, torchRange, torchIntensity, true);

            var fill = transform.Find("TorchFill");
            if (fill != null)
            {
                var fillLight = fill.GetComponent<Light>();
                if (fillLight != null)
                    ApplyTorch(fillLight, torchRange * 1.45f, torchIntensity * 0.32f, false);
            }

            if (flicker != null)
                flicker.Configure(torchRange, torchIntensity);
        }

        static void ApplyTorch(Light torch, float range, float intensity, bool shadows)
        {
            torch.range = range;
            torch.intensity = intensity;
            torch.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (!shadows)
                return;
            torch.shadowStrength = 0.82f;
            torch.shadowBias = 0.06f;
            torch.shadowNormalBias = 0.4f;
            torch.shadowNearPlane = 0.25f;
            var extra = torch.GetUniversalAdditionalLightData();
            extra.usePipelineSettings = false;
            extra.softShadowQuality = SoftShadowQuality.High;
        }

        void Update()
        {
            if (HoldPose)
                return;
            KeepOnFloor();
            if (!ControlEnabled || Time.timeScale <= 0f)
                return;

            Vector3 wish = ReadWish();
            float dt = Time.deltaTime;
            if (wish.sqrMagnitude > 0f)
            {
                Move(wish * (Speed * dt));
                stepTimer -= dt;
                if (stepTimer <= 0f)
                {
                    stepTimer = 0.42f;
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayFootstep();
                }
            }

            ResolveOverlaps();
            KeepOnFloor();
        }

        Vector3 ReadWish()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector3.zero;

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                horizontalPressedAt = Time.unscaledTime;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                verticalPressedAt = Time.unscaledTime;

            float x = 0f;
            float y = 0f;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) x += 1f;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) y -= 1f;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) y += 1f;
            if (Mathf.Abs(x) > 0f && Mathf.Abs(y) > 0f)
            {
                if (horizontalPressedAt >= verticalPressedAt)
                    y = 0f;
                else
                    x = 0f;
            }

            if (Mathf.Abs(x) > 0f) x = Mathf.Sign(x);
            if (Mathf.Abs(y) > 0f) y = Mathf.Sign(y);
            return new Vector3(x, 0f, y);
        }

        float Move(Vector3 delta)
        {
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 0.0001f)
                return 0f;
            Vector3 direction = delta / distance;
            Vector3 origin = Center;
            bool previous = body != null && body.enabled;
            if (body != null)
                body.enabled = false;
            bool blocked = Physics.SphereCast(origin, Radius * 0.92f, direction, out RaycastHit hit, distance + 0.02f, ~0, QueryTriggerInteraction.Ignore);
            if (body != null)
                body.enabled = previous;
            if (blocked)
                distance = Mathf.Max(0f, hit.distance - 0.015f);
            transform.position += direction * distance;
            KeepOnFloor();
            return distance;
        }

        void ResolveOverlaps()
        {
            if (body == null)
                return;
            Vector3 center = body.center;
            for (int pass = 0; pass < 3; pass++)
            {
                var hits = Physics.OverlapSphere(transform.position + center, Radius, ~0, QueryTriggerInteraction.Ignore);
                bool moved = false;
                for (int i = 0; i < hits.Length; i++)
                {
                    var other = hits[i];
                    if (other == null || other == body || other.isTrigger)
                        continue;
                    if (other.transform == transform || other.transform.IsChildOf(transform))
                        continue;
                    if (other.GetComponentInParent<BoulderController>() != null)
                        continue;
                    if (Physics.ComputePenetration(body, transform.position, transform.rotation, other, other.transform.position, other.transform.rotation, out Vector3 dir, out float dist))
                    {
                        dir.y = 0f;
                        if (dir.sqrMagnitude < 0.0001f)
                            continue;
                        transform.position += dir.normalized * (dist + 0.008f);
                        moved = true;
                    }
                }

                if (!moved)
                    break;
            }
        }

        void KeepOnFloor()
        {
            Vector3 p = transform.position;
            if (Mathf.Abs(p.y - GroundY) > 0.0001f)
            {
                p.y = GroundY;
                transform.position = p;
            }
        }

        public Vector3 Center
        {
            get
            {
                Vector3 c = transform.position + (body != null ? body.center : Vector3.zero);
                c.y = GroundY + (body != null ? body.center.y : 0f);
                return c;
            }
        }

        public float HitRadius => Radius;
    }
}
