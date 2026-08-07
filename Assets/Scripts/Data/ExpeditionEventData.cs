using System.Collections.Generic;
using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>事件房选项的效果类型 — 设计文档 §3.4「交易、救援、员工个人事件或路线选择」。</summary>
    public enum ExpeditionEventEffect
    {
        /// <summary>什么都不做，直接走人。</summary>
        Leave,

        /// <summary>交易：花金币换商品。</summary>
        Trade,

        /// <summary>搜刮：白拿商品，但小队要挨一下 —— 收益与代价成对出现。</summary>
        Scavenge
    }

    [System.Serializable]
    public class ExpeditionEventOption
    {
        public string label;
        public string detail;

        public ExpeditionEventEffect effect = ExpeditionEventEffect.Leave;

        /// <summary>Trade 用：花多少金币。</summary>
        public int coinCost;

        public string productId;
        public int productCount;

        /// <summary>Scavenge 用：每名队员掉多少血。</summary>
        public float squadDamage;

        public ExpeditionEventOption() { }

        public ExpeditionEventOption(string label, string detail, ExpeditionEventEffect effect)
        {
            this.label = label;
            this.detail = detail;
            this.effect = effect;
        }
    }

    /// <summary>
    /// 事件房的一次遭遇 — 设计文档 §3.4。
    ///
    /// 「救援新员工」也属于这一类，但那需要一套可增减的员工名册
    /// （§4「已招募员工」），要等 §18 第四阶段的双岗位系统落地，
    /// 所以这里先做交易与搜刮两种能立刻结算的。
    /// </summary>
    [CreateAssetMenu(fileName = "ExpeditionEventData", menuName = "MonsterStore/Expedition Event")]
    public class ExpeditionEventData : ScriptableObject
    {
        public string eventId;
        public string title;
        [TextArea(2, 5)] public string body;

        public List<ExpeditionEventOption> options = new List<ExpeditionEventOption>();
    }
}
