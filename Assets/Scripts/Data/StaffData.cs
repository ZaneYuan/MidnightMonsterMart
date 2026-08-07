using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>
    /// 怪物员工 — 设计文档 §4「怪物员工双岗位系统」。
    ///
    /// 文档的核心规则是「每只怪物必须同时拥有远征功能、店内功能、性格副作用三项」，
    /// 所以三块字段从一开始就放在同一个资产里。远征那部分在 §18 第一阶段就要用；
    /// 店内功能与副作用先以文本形式记录，等第四阶段做双岗位时再接上实际逻辑。
    /// </summary>
    [CreateAssetMenu(fileName = "StaffData", menuName = "MonsterStore/Staff")]
    public class StaffData : ScriptableObject
    {
        [Header("标识")]
        public string staffId;
        public string displayName;
        public MonsterType monsterType;

        [Header("远征 — 基础")]
        public float maxHealth = 60f;
        public float moveSpeed = 3.4f;

        /// <summary>跟随队长时保持的距离（§3.3「其他成员自动保持队形」）。</summary>
        public float followDistance = 1.5f;

        [Header("远征 — 普通攻击")]
        public float attackDamage = 8f;
        public float attackRange = 1.35f;
        public float attackInterval = 0.85f;

        [Header("远征 — 主动技能")]
        public string skillName;
        [TextArea(2, 4)] public string skillDescription;
        public float skillDamage = 22f;
        public float skillRadius = 2.6f;
        public float skillCooldown = 6f;

        public ElementTag element = ElementTag.None;

        /// <summary>
        /// 对精英与 Boss 的伤害倍率 — §4.2 吸血鬼·维拉的远征功能
        /// 「对精英怪额外伤害，低生命时吸血」。1 = 没有加成。
        /// </summary>
        public float eliteDamageMultiplier = 1f;

        [Header("远征被动 / 店内功能 / 副作用（§4.2）")]
        [TextArea(2, 4)] public string expeditionPassive;
        [TextArea(2, 4)] public string storeAbility;
        [TextArea(2, 4)] public string sideEffect;

        [Header("外观（灰盒阶段用色块）")]
        public Color bodyColor = Color.white;
        public Color accentColor = Color.gray;

        [System.NonSerialized] public Sprite runtimeSprite;
    }
}
