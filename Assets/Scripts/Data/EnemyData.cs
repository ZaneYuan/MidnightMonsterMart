using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>
    /// 远征地图上的敌人 — 设计文档 §3.5「暮光森林」。
    /// 普通敌人、精英和区域 Boss 共用这一张表，靠 <see cref="tier"/> 区分。
    ///
    /// §3.3 要求「敌人攻击必须有清晰前摇」，所以前摇时长是一等字段，
    /// 而不是藏在控制器里的魔数。同理，Boss 的区域机制参数也摊在这里，
    /// 而不是写死在 ExpeditionManager 里。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "MonsterStore/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [Header("标识")]
        public string enemyId;
        public string displayName;

        /// <summary>普通 / 精英 / Boss — §1.5 原型规模与 §3.4 精英房。</summary>
        public EnemyTier tier = EnemyTier.Normal;

        [Header("基础")]
        public float maxHealth = 40f;
        public float moveSpeed = 1.9f;

        /// <summary>发现目标的半径；超出后回到出生点附近游荡。</summary>
        public float aggroRadius = 7f;

        [Header("攻击")]
        public float attackDamage = 10f;
        public float attackRange = 1.2f;
        public float attackInterval = 1.7f;

        /// <summary>攻击前摇 — 设计文档 §3.3「敌人攻击必须有清晰前摇」。</summary>
        public float telegraphSeconds = 0.55f;

        [Header("护甲 — §3.4 精英房「风险较高」")]
        /// <summary>
        /// 对<b>普通攻击</b>的减伤比例（0~1）。技能和环境伤害不吃这一层。
        ///
        /// 精英的玩法本体就在这里：小队自动普攻打不动，玩家必须挑时机
        /// 放技能（§3.3「玩家负责走位、躲避预警区和主动技能时机」）。
        /// </summary>
        public float basicAttackResist;

        [Header("区域机制 · 孢子喷口 — §3.3「Boss 通过区域机制、护送商品或关闭装置制造变化」")]
        /// <summary>喷口数量。0 = 这个敌人没有区域机制。</summary>
        public int ventCount;

        /// <summary>喷口全部关闭后，隔多久重新喷发 —— 破防是窗口，不是一次性买断。</summary>
        public float ventReopenSeconds = 14f;

        /// <summary>单个喷口多久灼伤一次范围内的小队。</summary>
        public float ventPulseSeconds = 2.4f;

        public float ventPulseDamage = 7f;
        public float ventPulseRadius = 2.4f;

        /// <summary>还有喷口开着时，Boss 受到的伤害倍率 —— 不关装置就基本打不动。</summary>
        public float shieldedDamageMultiplier = 0.15f;

        public bool UsesSporeVents => ventCount > 0;

        [Header("掉落（§3.4 战斗房：清理敌人后获得普通商品）")]
        public string lootProductId;
        public int lootMin = 1;
        public int lootMax = 1;

        /// <summary>击败后直接掉的金币区间。</summary>
        public int coinMin = 2;
        public int coinMax = 5;

        [Header("Boss 奖励 — §3.4「掉落关键设施材料并解锁下一地区」/ §3.5")]
        /// <summary>冷藏货架核心 —— §3.5「区域 Boss：孢子巨兽；击败后获得冷藏货架核心」。</summary>
        public int coldShelfCores;

        /// <summary>击败后解锁的下一个供货区域 id（§2.1 阶段六「解锁灰烬火山、幽灵旧城等新区域」）。</summary>
        public string unlocksRegionId;
        public string unlocksRegionName;

        [Header("外观（灰盒阶段用色块）")]
        public Color bodyColor = Color.white;
        public Color accentColor = Color.gray;

        /// <summary>体型倍率 —— 灰盒阶段靠大小把精英和 Boss 一眼区分开。</summary>
        public float bodyScale = 1f;

        /// <summary>程序化角色贴图的外形编号，见 SpriteFactory —— 0~5 是顾客/玩家共用的编号，
        /// 6 以上是远征敌人专属的外形（蘑菇、荆棘、盗贼、守卫、巨兽）。</summary>
        public int silhouette;

        [Header("经验值 — 打怪升级")]
        /// <summary>击败后按存活小队均分的经验值。</summary>
        public float xpReward = 10f;

        [System.NonSerialized] public Sprite runtimeSprite;
    }
}
