using UnityEngine;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Store;

namespace MonsterMart.Customers
{
    /// <summary>吸血鬼 — 设计文档 §4.1。镜子、黑蒜面包、黑色袋子请求。</summary>
    public class VampireBehaviour : MonsterBehaviourBase
    {
        const float MirrorRange = 4.5f;
        const float MirrorDrainPerSecond = 6f;
        const float TabooRange = 2.6f;
        const float TabooDrainPerSecond = 4f;

        bool _bagRequestRolled;

        public override void OnUpdate(CustomerController customer)
        {
            customer.ExtraDecayMultiplier = 1f;

            // 镜子：靠近时持续掉耐心
            var mirror = Game.Store != null ? Game.Store.Mirror : null;
            if (mirror != null && mirror.AnnoysVampires)
            {
                float dist = Vector2.Distance(customer.Position, mirror.cells.CenterWorld);
                if (dist < MirrorRange)
                {
                    float falloff = 1f - dist / MirrorRange;
                    customer.ApplyPatience(-MirrorDrainPerSecond * falloff * Time.deltaTime);
                }
            }

            // 黑蒜面包摆在附近货架上也会惹恼它
            DrainNearTaboo(customer, MonsterType.Vampire, TabooRange, TabooDrainPerSecond);
        }

        public override void OnCheckout(CustomerController customer)
        {
            if (_bagRequestRolled) return;
            _bagRequestRolled = true;

            // 「请不要把血橙汽水装进透明袋子。」
            bool hasSoda = false;
            for (int i = 0; i < customer.Basket.Count; i++)
                if (customer.Basket[i].productId == "blood_orange_soda") hasSoda = true;

            if (hasSoda && Random.value < 0.75f)
                customer.WantsDiscreetBag = true;
        }

        internal static void DrainNearTaboo(CustomerController customer, MonsterType type,
                                            float range, float drainPerSecond)
        {
            var store = Game.Store;
            if (store == null) return;

            for (int i = 0; i < store.Shelves.Count; i++)
            {
                var shelf = store.Shelves[i];
                if (shelf.IsEmpty || shelf.product == null) continue;
                if (!shelf.product.IsDislikedBy(type)) continue;

                float dist = Vector2.Distance(customer.Position, shelf.cells.CenterWorld);
                if (dist >= range) continue;

                float falloff = 1f - dist / range;
                customer.ApplyPatience(-drainPerSecond * falloff * Time.deltaTime);
            }
        }
    }

    /// <summary>狼人 — 设计文档 §4.2。掉耐心快、会撞倒货架、满月夜情绪警告。</summary>
    public class WerewolfBehaviour : MonsterBehaviourBase
    {
        bool _hasCrashed;
        bool _warningRaised;
        float _rageTimer;

        public override void OnEnterStore(CustomerController customer)
        {
            var day = Game.Day != null ? Game.Day.CurrentPlan : null;
            if (day != null && day.fullMoon)
            {
                _warningRaised = true;
                _rageTimer = 20f;   // 玩家有 20 秒处理
                Game.Events?.OpenFullMoonWarning(customer);
            }
        }

        public override void OnUpdate(CustomerController customer)
        {
            customer.ExtraDecayMultiplier = _warningRaised ? 1.5f : 1f;

            VampireBehaviour.DrainNearTaboo(customer, MonsterType.Werewolf, 3.0f, 5f);

            if (_warningRaised)
            {
                _rageTimer -= Time.deltaTime;
                if (_rageTimer <= 0f)
                {
                    _warningRaised = false;
                    customer.ApplyPatience(-25f);
                    Game.UI?.Hud?.Flash("狼人的情绪失控了！");
                }
            }

            // 耐心低于 20 → 撞倒附近货架（文档 §7 事件二）
            if (!_hasCrashed &&
                customer.Patience < GameConfig.WerewolfCrashPatienceThreshold &&
                Game.Day != null && Game.Day.CurrentPlan != null &&
                Game.Day.CurrentPlan.allowShelfCrash)
            {
                _hasCrashed = true;
                Game.Events?.TriggerShelfCrash(customer);
            }
        }

        /// <summary>玩家成功安抚（给月光牛奶或关灯）时调用。</summary>
        public void CalmDown(CustomerController customer)
        {
            _warningRaised = false;
            _rageTimer = 0f;
            customer.ApplyPatience(30f);
            customer.AddSatisfaction(15f);
        }
    }

    /// <summary>幽灵 — 设计文档 §4.3。拿不到实体商品，需要灵界包装台。</summary>
    public class GhostBehaviour : MonsterBehaviourBase
    {
        bool _amnesiaRolled;

        public override bool RequiresSpiritPacking => true;

        public override void OnEnterStore(CustomerController customer)
        {
            if (_amnesiaRolled) return;
            _amnesiaRolled = true;

            var day = Game.Day != null ? Game.Day.CurrentPlan : null;
            if (day != null && day.allowGhostAmnesia && Random.value < 0.6f)
            {
                customer.AmnesiaActive = true;
                Game.UI?.Hud?.Flash($"{customer.Data.displayName} 好像忘记自己要买什么了……去和它聊聊");
            }
        }

        public override void OnUpdate(CustomerController customer)
        {
            customer.ExtraDecayMultiplier = 1f;
            // 驱灵盐摆出来会持续惹恼幽灵
            VampireBehaviour.DrainNearTaboo(customer, MonsterType.Ghost, 3.2f, 6f);
        }
    }

    /// <summary>史莱姆 — 设计文档 §4.4。留污渍、吞商品、分裂。</summary>
    public class SlimeBehaviour : MonsterBehaviourBase
    {
        float _stainTimer = 3.5f;
        bool _splitRolled;

        public override void OnUpdate(CustomerController customer)
        {
            customer.ExtraDecayMultiplier = 1f;

            if (customer.State == CustomerState.Leaving) return;

            _stainTimer -= Time.deltaTime;
            if (_stainTimer <= 0f)
            {
                _stainTimer = Random.Range(4.5f, 8f);
                Game.Store?.AddStain(customer.Cell, customer.Data.bodyColor);
            }
        }

        public override void OnTookProduct(CustomerController customer, ProductData product)
        {
            // 「史莱姆偶尔会吞下两件商品」
            if (Random.value < 0.3f && customer.RemainingBudget >= product.salePrice)
            {
                customer.SwallowedExtra++;
                Game.UI?.Hud?.Flash($"{customer.Data.displayName} 顺手吞下了多一件 {product.displayName}");
            }

            // 「史莱姆吃下发光果冻后小概率分裂」（文档 §7 事件四）
            if (_splitRolled) return;
            if (product.productId != "glow_jelly") return;

            var day = Game.Day != null ? Game.Day.CurrentPlan : null;
            if (day == null || !day.allowSlimeSplit) return;

            if (Random.value < GameConfig.SlimeSplitChance)
            {
                _splitRolled = true;
                Game.Events?.TriggerSlimeSplit(customer);
            }
        }
    }

    /// <summary>神秘检查员 — 设计文档 §7 事件五。结账时给出评级。</summary>
    public class InspectorBehaviour : MonsterBehaviourBase
    {
        public override void OnEnterStore(CustomerController customer)
        {
            Game.UI?.Hud?.Flash("一位穿风衣的客人走了进来……");
        }

        public override void OnUpdate(CustomerController customer)
        {
            customer.ExtraDecayMultiplier = 1f;
        }

        public override void OnCheckout(CustomerController customer)
        {
            Game.Events?.RunInspection(customer);
        }

        public override void OnLeaveStore(CustomerController customer)
        {
            // 他要买的东西缺货、或者被气走了 —— 检查照样要出结果
            if (Game.Day != null && !Game.Day.InspectionDone)
                Game.Events?.RunInspection(customer);
        }
    }
}
