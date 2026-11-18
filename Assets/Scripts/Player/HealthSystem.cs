using System;
using UnityEngine;

namespace DrJones
{
    public sealed class HealthSystem : MonoBehaviour
    {
        public int Current { get; private set; }
        public int Max { get; private set; }
        public bool IsDead => Current <= 0;

        public event Action Died;
        public event Action<int, int> Changed;

        public void Configure(int max)
        {
            Max = Mathf.Max(1, max);
            Current = Max;
            Changed?.Invoke(Current, Max);
        }

        public void Restore()
        {
            Current = Max;
            Changed?.Invoke(Current, Max);
        }

        public bool Heal(int amount)
        {
            if (IsDead || amount <= 0 || Current >= Max)
                return false;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke(Current, Max);
            return true;
        }

        public bool TryDamage(int amount, InvulnerabilitySystem invulnerability)
        {
            if (IsDead || amount <= 0)
                return false;
            if (GameManager.Instance != null && GameManager.Instance.Invincible)
                return false;
            if (invulnerability != null && invulnerability.IsActive)
                return false;

            Current = Mathf.Max(0, Current - amount);
            Changed?.Invoke(Current, Max);
            if (Current <= 0)
                Died?.Invoke();
            else
                invulnerability?.Trigger();
            return true;
        }
    }
}
