using UnityEngine;

namespace DrJones
{
    public sealed class TorchFlicker : MonoBehaviour
    {
        public float BaseIntensity = 14f;
        public float BaseRange = 6.5f;

        Light point;

        void Awake()
        {
            point = GetComponent<Light>();
            if (point != null)
            {
                BaseIntensity = point.intensity;
                BaseRange = point.range;
            }
        }

        public void Configure(float range, float intensity)
        {
            BaseRange = range;
            BaseIntensity = intensity;
        }

        void Update()
        {
            if (point == null)
                return;
            float n = Mathf.PerlinNoise(Time.time * 6.5f, 0.2f);
            point.intensity = BaseIntensity * Mathf.Lerp(0.82f, 1.12f, n);
            point.range = BaseRange * Mathf.Lerp(0.94f, 1.04f, n);
        }
    }
}
