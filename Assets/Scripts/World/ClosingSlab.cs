using UnityEngine;

namespace DrJones
{
    public sealed class ClosingSlab : MonoBehaviour
    {
        Vector3 open;
        Vector3 shut;
        float duration = 8f;
        float elapsed;
        bool running;
        bool hold;
        ParticleSystem dust;

        public bool Sealed => elapsed >= duration - 0.02f;
        public float Remaining => Mathf.Max(0f, duration - elapsed);

        public void Begin(float seconds)
        {
            duration = Mathf.Max(0.75f, seconds);
            elapsed = 0f;
            running = true;
            hold = false;
            open = transform.position;
            shut = new Vector3(open.x, 0f, open.z);
            dust = VisualFactory.Dust(transform);
            if (dust != null)
            {
                var shape = dust.shape;
                shape.radius = 0.42f;
                dust.Play();
            }
        }

        public void HoldGap()
        {
            hold = true;
            if (dust != null)
                dust.Stop();
        }

        public System.Collections.IEnumerator Slam(float time)
        {
            hold = false;
            running = false;
            Vector3 from = transform.position;
            time = Mathf.Max(0.08f, time);
            float t = 0f;
            if (dust != null && !dust.isPlaying)
                dust.Play();
            while (t < time)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
                transform.position = Vector3.Lerp(from, shut, u);
                yield return null;
            }

            transform.position = shut;
            elapsed = duration;
            if (dust != null)
                dust.Stop();
            VisualFactory.Burst(transform.position + Vector3.up * 0.4f, new Color(0.75f, 0.62f, 0.42f, 0.8f), 16);
        }

        void Update()
        {
            if (!running || hold || Time.timeScale <= 0f)
                return;
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(open, shut, u * u);
        }
    }
}
