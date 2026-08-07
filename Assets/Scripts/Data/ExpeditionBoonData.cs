using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>
    /// 轻度肉鸽三选一强化 — 设计文档 §3.6。
    ///
    /// 文档给了两条硬约束，这个类的形状就是照着它们来的：
    ///   1.「<b>仅在本次远征生效</b>」—— 所以强化不进存档，也不挂在员工身上，
    ///      而是由 ExpeditionManager 持有，Begin() 时清空。
    ///   2.「强化优先提供<b>收益与代价</b>，而不是无脑增加伤害」—— 所以每个字段
    ///      都成对出现，冒烟测试会逐条检查「有收益必有代价」。
    ///
    /// 所有效果字段都是倍率或增量，缺省值 = 没有效果，
    /// 这样叠加多个强化时直接连乘 / 累加即可。
    /// </summary>
    [CreateAssetMenu(fileName = "ExpeditionBoonData", menuName = "MonsterStore/ExpeditionBoon")]
    public class ExpeditionBoonData : ScriptableObject
    {
        [Header("标识")]
        public string boonId;
        public string displayName;

        [TextArea(2, 3)] public string benefit;
        [TextArea(2, 3)] public string cost;

        [Header("批发契约")]
        /// <summary>普通敌人掉落倍率。</summary>
        public float normalLootMultiplier = 1f;

        /// <summary>Boss 掉落倍率 —— §3.6「Boss 奖励品质下降」。</summary>
        public float bossLootMultiplier = 1f;

        [Header("加班狂热")]
        /// <summary>技能冷却倍率，小于 1 就是缩短。</summary>
        public float skillCooldownMultiplier = 1f;

        /// <summary>
        /// 每次放技能的自损生命。
        ///
        /// §3.6 原文的代价是「远征结束后获得额外疲劳」，但疲劳系统属 §4.4，
        /// 要等 §18 第四阶段的双岗位才做得出来。这里先用「透支自己的血」
        /// 当灰盒替身，保住「收益与代价成对」这条设计约束；
        /// 疲劳做出来之后把这一项换回去即可。
        /// </summary>
        public float skillSelfDamage;

        [Header("史莱姆快递")]
        /// <summary>拾取范围倍率。</summary>
        public float pickupRadiusMultiplier = 1f;

        /// <summary>史莱姆员工的攻击力倍率 —— §3.6「史莱姆携带货物时攻击力下降」。</summary>
        public float slimeAttackMultiplier = 1f;

        [Header("易碎品保险")]
        /// <summary>被击退时额外保留的战利品比例（叠加在 §3.7 的基础保留率上）。</summary>
        public float failKeepRatioBonus;

        /// <summary>队长移动速度倍率 —— §3.6「但移动速度降低」。</summary>
        public float captainSpeedMultiplier = 1f;

        /// <summary>这个强化给了玩家什么。</summary>
        public bool HasBenefit =>
            normalLootMultiplier > 1f ||
            skillCooldownMultiplier < 1f ||
            pickupRadiusMultiplier > 1f ||
            failKeepRatioBonus > 0f;

        /// <summary>这个强化让玩家付出了什么 —— §3.6 要求两者成对。</summary>
        public bool HasCost =>
            bossLootMultiplier < 1f ||
            skillSelfDamage > 0f ||
            slimeAttackMultiplier < 1f ||
            captainSpeedMultiplier < 1f;
    }
}
