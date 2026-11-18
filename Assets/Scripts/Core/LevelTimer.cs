using UnityEngine;

namespace DrJones
{
    public sealed class LevelTimer : MonoBehaviour
    {
        public float Remaining { get; private set; }
        public float Total { get; private set; }
        public bool Running { get; private set; }

        public void BeginLevel(float seconds)
        {
            Remaining = seconds;
            Running = true;
        }

        public void Stop()
        {
            Running = false;
        }

        public void SetTotal(float seconds)
        {
            Total = Mathf.Max(0f, seconds);
        }

        void Update()
        {
            if (!Running || Time.timeScale <= 0f || (GameManager.Instance != null && GameManager.Instance.SimulationFrozen))
                return;
            float dt = Time.deltaTime;
            Remaining -= dt;
            Total += dt;
            if (Remaining > 0f)
                return;
            Remaining = 0f;
            Running = false;
            if (GameManager.Instance != null)
                GameManager.Instance.Fail(FailReason.Time);
        }
    }
}
