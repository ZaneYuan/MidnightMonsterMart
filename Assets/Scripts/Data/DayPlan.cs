using System.Collections.Generic;
using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>营业日里的一个顾客生成条目。</summary>
    [System.Serializable]
    public class SpawnEntry
    {
        public MonsterType monsterType;

        [Tooltip("从营业开始算起的入店时间（秒）")]
        public float atSeconds;

        public SpawnEntry() { }

        public SpawnEntry(MonsterType type, float at)
        {
            monsterType = type;
            atSeconds = at;
        }
    }

    /// <summary>
    /// 单个营业日的配置 — 设计文档 §8「三天原型流程」。
    /// </summary>
    [CreateAssetMenu(fileName = "DayPlan", menuName = "MonsterStore/Day Plan")]
    public class DayPlan : ScriptableObject
    {
        [Header("标识")]
        public int dayNumber = 1;
        public string title;
        [TextArea(2, 6)] public string briefing;

        [Header("节奏")]
        public float businessSeconds = 200f;

        [Tooltip("当天每位顾客最多买几件（教学日调低，避免玩家来不及补货）")]
        public int maxItemsPerCustomer = 3;

        [Header("顾客波次")]
        public List<SpawnEntry> spawns = new List<SpawnEntry>();

        [Header("当日目标")]
        public int goalCustomersServed;
        public int goalMinProfit;
        public int goalMinReputation;
        public int goalMinCleanliness;

        /// <summary>
        /// 当天最多允许被狼人撞倒几个货架；-1 表示没有这项目标。
        /// 用 -1 而不是 0 当哨兵，因为 0 本身是「一个都不许倒」这个合法目标。
        /// 设计文档 §10 第二天：「不让狼人破坏超过一个货架」。
        /// </summary>
        public int goalMaxShelvesKnocked = -1;

        [TextArea(2, 6)] public string goalDescription;

        [Header("启用的事件")]
        public bool allowBlackout;
        public bool allowShelfCrash;
        public bool allowGhostAmnesia;
        public bool allowSlimeSplit;
        public bool spawnInspector;

        [Tooltip("满月夜：狼人耐心衰减更快，且入店即触发情绪警告")]
        public bool fullMoon;

        public int TotalCustomers => spawns.Count;
    }
}
