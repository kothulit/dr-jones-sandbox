using UnityEngine;

namespace DrJones
{
    public sealed class HeartPickup : MonoBehaviour
    {
        public float PickupRadius = 0.55f;
        public int Amount = 1;

        float phase;

        void Awake()
        {
            phase = Random.value * 6f;
        }

        void Update()
        {
            if (Time.timeScale <= 0f)
                return;
            phase += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(phase * 4f) * 0.08f;
            transform.localScale = Vector3.one * pulse;

            var game = GameManager.Instance;
            var player = game != null ? game.Player : null;
            if (player == null)
                return;
            Vector3 offset = transform.position - player.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > PickupRadius * PickupRadius)
                return;
            var health = player.GetComponent<HealthSystem>();
            if (health == null || !health.Heal(Amount))
                return;
            VisualFactory.Burst(transform.position, new Color(1f, 0.3f, 0.3f, 0.9f), 14);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayPickup();
            Destroy(gameObject);
        }
    }
}
