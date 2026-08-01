namespace MonsterMart.Data
{
    /// <summary>游戏总状态机 — 对应设计文档 §12.1。</summary>
    public enum GameState
    {
        Preparation,
        Open,
        Settlement,
        Paused,
        GameOver
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
