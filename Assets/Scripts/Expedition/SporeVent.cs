using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.Expeditions
{
    /// <summary>
    /// 孢子喷口 — 孢子巨兽的区域机制，对应设计文档 §3.3
    /// 「Boss 通过区域机制、护送商品或<b>关闭装置</b>制造变化」。
    ///
    /// 规则：
    ///   · 只要还有喷口开着，Boss 就几乎打不动（EnemyData.shieldedDamageMultiplier）。
    ///   · 开着的喷口会周期性灼伤范围内的小队，站着不动就会被磨死。
    ///   · 队长走到喷口上按 E 关掉它 —— 也就是 §3.2 交互键里的「开箱 / 关闭装置」。
    ///   · 全关之后 Boss 破防一段时间，随后重新喷发。破防是<b>窗口</b>，不是买断，
    ///     所以整场 Boss 战是「关装置 → 集火 → 再关」的节奏循环。
    ///
    /// 帧循环不能作为唯一驱动：冒烟测试跑在编辑器非播放模式下，没有 Update。
    /// 所以推进逻辑抽成 <see cref="TickPulse"/>，Update 只是把 Time.deltaTime 喂进去。
    /// </summary>
    public class SporeVent : MonoBehaviour
    {
        /// <summary>队长要站多近才能关掉它。和采集点同一个手感。</summary>
        public const float CloseRadius = 1.2f;

        public bool IsOpen { get; private set; }
        public Vector2 Position => _position;

        /// <summary>距离下一次灼伤还有多久（关着时无意义）。</summary>
        public float PulseCountdown => _pulseTimer;

        Vector2 _position;
        float _pulseSeconds;
        float _pulseDamage;
        float _pulseRadius;
        float _pulseTimer;

        SpriteRenderer _ring;
        SpriteRenderer _core;

        public float PulseRadius => _pulseRadius;
        public float PulseDamage => _pulseDamage;

        public void Initialize(Vector2 localPosition, EnemyData boss)
        {
            _position = localPosition;
            _pulseSeconds = Mathf.Max(0.2f, boss.ventPulseSeconds);
            _pulseDamage = Mathf.Max(0f, boss.ventPulseDamage);
            _pulseRadius = Mathf.Max(0.1f, boss.ventPulseRadius);

            transform.localPosition = _position;

            var ringGo = new GameObject("Cloud");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localScale = Vector3.one * _pulseRadius * 2f;
            _ring = ringGo.AddComponent<SpriteRenderer>();
            _ring.sprite = SpriteFactory.Circle(new Color(0.62f, 0.85f, 0.35f, 0.22f), 40);
            _ring.sortingOrder = SortingLayers.Floor + 2;

            var coreGo = new GameObject("Vent");
            coreGo.transform.SetParent(transform, false);
            coreGo.transform.localScale = Vector3.one * 0.7f;
            _core = coreGo.AddComponent<SpriteRenderer>();
            _core.sprite = SpriteFactory.Circle(new Color(0.75f, 0.95f, 0.40f, 0.85f), 32);
            _core.sortingOrder = SortingLayers.Floor + 3;

            Open();
        }

        public void Open()
        {
            IsOpen = true;
            _pulseTimer = _pulseSeconds;
            RefreshVisual();
        }

        public void Close()
        {
            IsOpen = false;
            RefreshVisual();
        }

        void RefreshVisual()
        {
            if (_ring != null)
                _ring.color = IsOpen
                    ? new Color(0.62f, 0.85f, 0.35f, 0.22f)
                    : new Color(0.35f, 0.40f, 0.38f, 0.08f);

            if (_core != null)
                _core.color = IsOpen
                    ? new Color(0.75f, 0.95f, 0.40f, 0.85f)
                    : new Color(0.36f, 0.40f, 0.36f, 0.45f);
        }

        public bool InRange(Vector2 from) => (from - _position).magnitude <= CloseRadius;

        /// <summary>
        /// 推进灼伤计时。返回这一步有没有真的喷一次。
        /// 抽出来是为了让无头用例能驱动真实逻辑（见「可测试性抽取模式」）。
        /// </summary>
        public bool TickPulse(float dt)
        {
            if (!IsOpen || dt <= 0f) return false;

            _pulseTimer -= dt;
            if (_pulseTimer > 0f) return false;

            _pulseTimer = _pulseSeconds;
            ApplyPulse();
            return true;
        }

        /// <summary>灼伤范围内所有还活着的队员 —— 队长也算。</summary>
        public void ApplyPulse()
        {
            if (_pulseDamage <= 0f) return;

            var expedition = Game.Expedition;
            if (expedition == null) return;

            var allies = expedition.Allies;
            for (int i = 0; i < allies.Count; i++)
            {
                var actor = allies[i];
                if (actor == null || !actor.IsAlive) continue;
                if ((actor.Position - _position).magnitude > _pulseRadius) continue;

                actor.Health.Damage(_pulseDamage);
            }
        }

        void Update()
        {
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;
            if (!IsOpen) return;

            TickPulse(Time.deltaTime);

            // 呼吸感：越接近下一次喷发越亮
            if (_ring != null && _pulseSeconds > 0f)
            {
                float t = 1f - Mathf.Clamp01(_pulseTimer / _pulseSeconds);
                _ring.color = new Color(0.62f, 0.85f, 0.35f, 0.16f + 0.22f * t);
            }
        }
    }
}
