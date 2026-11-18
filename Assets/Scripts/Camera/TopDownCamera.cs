using UnityEngine;

namespace DrJones
{
    public sealed class TopDownCamera : MonoBehaviour
    {
        public static TopDownCamera Instance { get; private set; }

        public float ViewWidth = 20f;
        public float Height = 16f;
        public Transform Target;

        Camera cam;
        float shakeTime;
        float shakeAmp;
        bool snapped;
        bool steering;
        bool holding;
        Vector3 holdPoint;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            ApplyLens();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Shake(float amplitude, float duration)
        {
            shakeAmp = amplitude;
            shakeTime = duration;
        }

        public void ReleaseHold()
        {
            holding = false;
        }

        public System.Collections.IEnumerator Pan(Vector3 world, float duration)
        {
            steering = true;
            holding = false;
            Vector3 from = transform.position;
            Vector3 to = LookPoint(world);
            float time = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (time < duration)
            {
                time += Time.deltaTime;
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
                transform.position = Vector3.Lerp(from, to, blend);
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                yield return null;
            }

            transform.position = to;
            holdPoint = world;
            holding = true;
            snapped = true;
            steering = false;
        }

        void LateUpdate()
        {
            if (cam == null)
                return;
            ApplyLens();
            if (steering)
                return;
            Vector3 look = holding ? holdPoint : (Target != null ? Target.position : transform.position);
            if (Target == null && !holding)
                return;
            Vector3 desired = LookPoint(look);
            if (!snapped)
            {
                transform.position = desired;
                snapped = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, desired, t);
            }

            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                Vector3 shake = Random.insideUnitSphere * shakeAmp;
                shake.y = 0f;
                transform.position += shake;
            }
        }

        void ApplyLens()
        {
            cam.orthographic = false;
            float aspect = Mathf.Max(0.1f, cam.aspect);
            cam.fieldOfView = 2f * Mathf.Atan((ViewWidth * 0.5f) / (Height * aspect)) * Mathf.Rad2Deg;
        }

        Vector3 LookPoint(Vector3 world)
        {
            return new Vector3(world.x, Height, world.z);
        }
    }
}
