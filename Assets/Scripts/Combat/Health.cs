using System;
using UnityEngine;

namespace MonsterMart.Combat
{
    /// <summary>
    /// 生命值 — 队长、怪物员工和敌人共用。
    /// 设计文档 §3.3「受击与倒地」；伤害数字与命中反馈由订阅方负责表现。
    /// </summary>
    public class Health : MonoBehaviour
    {
        public float Max { get; private set; } = 1f;
        public float Current { get; private set; } = 1f;

        public bool IsDead => Current <= 0f;
        public float Normalized => Max <= 0f ? 0f : Mathf.Clamp01(Current / Max);

        /// <summary>参数是实际扣掉的血量。</summary>
        public event Action<float> OnDamaged;
        public event Action OnDied;

        public void Initialize(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        public void Damage(float amount)
        {
            if (amount <= 0f || IsDead) return;

            float before = Current;
            Current = Mathf.Max(0f, Current - amount);

            OnDamaged?.Invoke(before - Current);
            if (Current <= 0f) OnDied?.Invoke();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead) return;
            Current = Mathf.Min(Max, Current + amount);
        }
    }
}
