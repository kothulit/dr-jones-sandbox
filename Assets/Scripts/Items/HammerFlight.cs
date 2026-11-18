using System;
using UnityEngine;

namespace DrJones
{
    public sealed class HammerFlight : MonoBehaviour
    {
        Vector3 target;
        Action onArrive;
        float speed = 9f;
        bool flying;
        Light flash;

        public bool Arrived { get; private set; }

        public void Launch(Vector3 worldTarget, float flightSpeed, Action arrived)
        {
            target = worldTarget;
            target.y = transform.position.y;
            speed = flightSpeed;
            onArrive = arrived;
            flying = false;
            flash = GetComponent<Light>();
            if (flash != null)
                flash.intensity = 10f;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayHammer();
            Invoke(nameof(BeginFlight), 0.55f);
        }

        void BeginFlight()
        {
            flying = true;
        }

        void Update()
        {
            if (!flying)
            {
                transform.Rotate(0f, 40f * Time.deltaTime, 0f);
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            transform.Rotate(0f, 240f * Time.deltaTime, 0f);
            if ((transform.position - target).sqrMagnitude > 0.002f)
                return;

            flying = false;
            Arrived = true;
            VisualFactory.Burst(transform.position, new Color(0.7f, 0.95f, 1f, 1f), 28);
            onArrive?.Invoke();
            if (flash != null)
                flash.intensity = 2.5f;
        }
    }
}
