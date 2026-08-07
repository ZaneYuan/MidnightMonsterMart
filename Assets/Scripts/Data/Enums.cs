namespace MonsterMart.Data
{
    /// <summary>
    /// 游戏总状态机 — 对应设计文档 v0.2 §14.1。
    ///
    /// 文档列了 8 个状态；这里按开发阶段逐个补齐，暂缺 MorningBrief 和
    /// ExpeditionPreparation（属 §18 第二阶段）。Preparation 即文档的
    /// StorePreparation（闭店准备），等晨会做出来再一并改名。
    /// 新状态一律追加在末尾，避免动到已有值。
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// 闭店准备（文档的 StorePreparation）：进货、上架、安排夜班。
        /// 名字沿用至今是历史原因，改名要连带动一批引用，等 UI 大改时再说。
        /// </summary>
        Preparation,
        Open,
        Settlement,
        Paused,
        GameOver,

        /// <summary>白天异世界进货 — 设计文档 §3。</summary>
        Expedition,

        /// <summary>
        /// 晨间需求与排班 — 设计文档 §2.1 阶段一。
        /// 一天从这里开始：看昨夜缺货与今晚订单，决定谁出征、谁值夜班。
        /// 新状态一律追加在末尾。
        /// </summary>
        MorningBrief
    }

    /// <summary>
    /// 夜班岗位 — 设计文档 §4.3「店内岗位」。
    ///
    /// 刻意<b>不</b>把「远征」放进这个枚举：§4.4 的核心张力是
    /// 「白天远征后继续值夜班会快速累积疲劳」，也就是说一只怪物可以
    /// <b>既出征又值夜班</b>。两者是两个独立的轴，塞进同一个枚举
    /// 就等于规定了「出征的人晚上必须休息」，那条张力就没了。
    ///
    /// 厨房（§4.3 第四个岗位）暂缺：原型里还没有热食系统，先不放进来，
    /// 免得玩家排了一个什么都不会发生的岗位。
    /// </summary>
    public enum StaffAssignment
    {
        /// <summary>不值夜班。没出征的话还能回复疲劳。</summary>
        Rest,
        /// <summary>收银：结账更快、排队更耐烦。</summary>
        Cashier,
        /// <summary>补货：营业中自动从仓库往货架搬。</summary>
        Restock,
        /// <summary>安保：压住狼人撞货架这类事故。</summary>
        Security
    }

    /// <summary>员工元素标签 — 设计文档 §3.3「每名员工拥有……元素标签」。</summary>
    public enum ElementTag
    {
        None,
        Toxic,    // 史莱姆：毒
        Beast,    // 狼人：兽
        Spirit,   // 幽灵：灵
        Blood     // 吸血鬼：血
    }

    /// <summary>远征房间类型 — 设计文档 §3.4「地图房间结构」。</summary>
    public enum RoomKind
    {
        Camp,       // 入口营地：查看目标、队伍和撤退规则
        Resource,   // 资源房：低压力采集，强调路线和携带容量
        Battle,     // 战斗房：清理敌人后获得普通商品
        Event,      // 事件房：交易、救援、员工个人事件或路线选择
        Elite,      // 精英房：风险较高，产出稀有商品、工具或员工装备
        Boss        // Boss 房：结算区域机制，掉落关键设施材料并解锁下一地区
    }

    /// <summary>
    /// 敌人分级 — 设计文档 §1.5 原型规模「3 种普通敌人 + 1 个区域 Boss」，
    /// 精英则来自 §3.4「精英房：风险较高，产出稀有商品」。
    ///
    /// 分级不只是数值高低：§4.2 吸血鬼·维拉的远征被动是「对精英怪额外伤害」，
    /// 需要一个能被判定的标签。
    /// </summary>
    public enum EnemyTier
    {
        Normal,
        Elite,
        Boss
    }

    /// <summary>
    /// 伤害来源 — 精英护甲只吃普通攻击，技能打满
    /// （§3.3「玩家负责走位、躲避预警区和主动技能时机」）。
    /// </summary>
    public enum DamageKind
    {
        /// <summary>自动普通攻击。</summary>
        Basic,
        /// <summary>玩家按 1~3 主动放的技能。</summary>
        Skill,
        /// <summary>环境伤害（孢子喷口等），不吃任何减伤。</summary>
        Environment
    }

    /// <summary>远征结束方式 — 设计文档 §3.7「失败与撤退」。</summary>
    public enum ExpeditionOutcome
    {
        None,
        Cleared,     // 清空房间
        Retreated,   // 主动撤退，保留更多商品
        Failed       // 小队倒下，损失部分易碎商品
    }

    /// <summary>商品分类 — 设计文档 §2.2「原型阶段只需要四类」。</summary>
    public enum ProductCategory
    {
        Drink,
        Food,
        Snack,
        Tool
    }

    /// <summary>怪物种类 — 设计文档 §12.3。</summary>
    public enum MonsterType
    {
        Vampire,
        Werewolf,
        Ghost,
        Slime,
        Inspector
    }

    /// <summary>顾客有限状态机 — 设计文档 §12.4。</summary>
    public enum CustomerState
    {
        Entering,
        ChoosingProduct,
        MovingToShelf,
        TakingProduct,
        MovingToCheckout,
        WaitingInQueue,
        CheckingOut,
        Leaving,
        Angry,
        SpecialEvent
    }

    /// <summary>耐心分段 — 设计文档 §3.4「顾客耐心值」。</summary>
    public enum PatienceTier
    {
        Calm,        // 60-100
        Impatient,   // 30-59
        Complaining, // 1-29
        Exhausted    // 0
    }

    /// <summary>店铺固定设施种类。</summary>
    public enum FixtureKind
    {
        Shelf,
        Cooler,
        ToolRack,
        Checkout,
        StockRoom,
        SpiritPackingStation,
        Mirror,
        TrashBin,
        Door
    }

    /// <summary>检查员评级 — 设计文档 §7 事件五。</summary>
    public enum InspectionGrade
    {
        A,
        B,
        C,
        Suspended
    }

    /// <summary>三天原型的结局 — 设计文档 §8。</summary>
    public enum EndingType
    {
        None,
        Excellent,
        Normal,
        Failure
    }
}
