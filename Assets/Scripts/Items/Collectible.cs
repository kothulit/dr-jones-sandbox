using System;
using UnityEngine;

namespace DrJones
{
    public enum CollectibleKind
    {
        FertilityIdol,
        ThorFragment
    }

    public sealed class Collectible : MonoBehaviour
    {
        public int Index;
        public CollectibleKind Kind;
        public float PickupRadius = 0.55f;

        public event Action<Collectible> Picked;

        bool taken;
        PlayerController player;

        public void Setup(int index, CollectibleKind kind)
        {
            Index = index;
            Kind = kind;
        }

        void Update()
        {
            if (taken || Time.timeScale <= 0f)
                return;
            if (player == null)
                player = FindAnyObjectByType<PlayerController>();
            if (player == null)
                return;

            Vector3 a = transform.position;
            Vector3 b = player.transform.position;
            a.y = 0f;
            b.y = 0f;
            if ((a - b).sqrMagnitude > PickupRadius * PickupRadius)
                return;

            taken = true;
            VisualFactory.Burst(transform.position, Kind == CollectibleKind.FertilityIdol
                ? new Color(1f, 0.85f, 0.3f, 0.9f)
                : new Color(0.5f, 0.9f, 1f, 0.9f), 16);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayPickup();
            Picked?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
