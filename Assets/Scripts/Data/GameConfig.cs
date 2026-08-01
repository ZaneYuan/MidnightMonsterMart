namespace MonsterMart.Data
{
    /// <summary>
    /// 全局数值调参表。原型阶段所有可调数值集中在这里，方便手感调试。
    /// 数值来源于设计文档；文档未给定的取值在注释中标注为「补充」。
    /// </summary>
    public static class GameConfig
    {
        // ---------- 网格与场景（设计文档 §9.2） ----------
        public const int GridWidth = 24;
        public const int GridHeight = 16;
        public const int PixelsPerUnit = 32;   // 单格 32×32 像素 → 1 格 = 1 世界单位

        /// <summary>内部可行走区域 20×12，因此四周墙体各 2 格厚。</summary>
        public const int WallThickness = 2;

        public const int InteriorMinX = WallThickness;
        public const int InteriorMaxX = GridWidth - WallThickness - 1;   // 21
        public const int InteriorMinY = WallThickness;
        public const int InteriorMaxY = GridHeight - WallThickness - 1;  // 13

        // ---------- 玩家（设计文档 §3.1） ----------
        public const float PlayerWalkSpeed = 4.2f;
        public const float PlayerSprintSpeed = 7.0f;
        public const float PlayerRadius = 0.32f;
        public const int PlayerCarryCapacity = 5;      // 「一次只携带一种商品，最大 5」
        public const float InteractRange = 1.35f;
        public const float RestockSecondsPerItem = 0.35f;  // 补货时无法移动
        public const float CleanStainSeconds = 1.6f;
        public const float LiftShelfSeconds = 3.0f;        // §7 事件二「连续交互 3 秒扶起货架」

        /// <summary>踩到污渍时的移动速度倍率（§4.4「让玩家移动速度下降」）。</summary>
        public const float StainSlowMultiplier = 0.55f;

        // ---------- 货架（设计文档 §3.3） ----------
        public const int ShelfCapacity = 8;            // 文档建议 6~10

        // ---------- 顾客（设计文档 §3.4） ----------
        public const float PatienceCalmThreshold = 60f;
        public const float PatienceImpatientThreshold = 30f;
        public const float PatienceComplainThreshold = 1f;

        /// <summary>缺货时顾客等待多久才放弃（留出玩家跑一趟仓库的时间）。</summary>
        public const float OutOfStockWaitSeconds = 14f;

        /// <summary>整洁度低于 50 时，所有顾客耐心衰减倍率（§6.3）。</summary>
        public const float DirtyStoreDecayMultiplier = 1.6f;

        /// <summary>整洁度低于 20 时，顾客有概率直接离店（§6.3）。</summary>
        public const float FilthyStoreLeaveChancePerSecond = 0.04f;

        public const float CustomerArriveDistance = 0.12f;
        public const float BrowseSeconds = 1.1f;

        // ---------- 收银（设计文档 §5） ----------
        public const float ScanBaseWindow = 1.0f;       // 初级收银台扫描判定区宽度
        public const float ScanUpgradedWindow = 1.7f;   // 升级后判定更大
        public const int CheckoutUpgradeCost = 100;     // 文档明确 100 金币
        public const float QueuePatiencePenaltyPerSecond = 0.9f;
        public const float UpgradedQueuePatienceMultiplier = 0.6f;
        public const int MissedScanSatisfactionPenalty = 8;
        public const int DoubleScanSatisfactionPenalty = 12;
        public const int QueuePointCount = 3;           // §9.3「收银台前设置 3 个排队点」

        // ---------- 店铺指标（设计文档 §6） ----------
        public const int StartingMoney = 90;
        public const int StartingReputation = 30;
        public const float StartingCleanliness = 100f;

        public const float CleanlinessMin = 0f;
        public const float CleanlinessMax = 100f;
        public const int ReputationMin = 0;
        public const int ReputationMax = 100;

        public const float CleanlinessDirtyThreshold = 50f;
        public const float CleanlinessFilthyThreshold = 20f;

        public const float StainCleanlinessCost = 7f;
        public const float ShelfCrashCleanlinessCost = 20f;   // §7 事件二明确 -20

        // 声望增减（文档 §6.2 给出方向，具体数值为补充）
        public const int RepHappyCustomer = 3;
        public const int RepPerfectSpecialRequest = 4;
        public const int RepAngryCustomer = -6;
        public const int RepOutOfStock = -2;
        public const int RepTabooViolation = -4;
        public const int RepScanError = -2;

        // ---------- 事件（设计文档 §7） ----------
        public const float BlackoutDuration = 30f;          // 「等待 30 秒自动恢复」
        public const int BlackoutGeneratorCost = 10;        // 「花费 10 金币」
        public const float BlackoutMoveSpeedMultiplier = 0.7f;
        public const float WerewolfCrashPatienceThreshold = 20f;  // 「耐心低于 20」
        public const float SlimeSplitChance = 0.35f;
        public const int SlimeSplitCount = 2;

        // ---------- 结局判定（设计文档 §8） ----------
        public const int EndingExcellentReputation = 70;
        public const int EndingExcellentProfit = 150;
        public const int EndingFailureReputation = 20;

        // ---------- 存档 ----------
        public const string SaveFileName = "midnight_monster_mart_save.json";
        /// <summary>
        /// 改动起始资金 / 难度数值后要 +1 —— 旧存档会被忽略，
        /// 否则玩家会带着上一版的钱进新版本（比如身上 0 块钱开局，直接卡死）。
        /// </summary>
        public const int SaveVersion = 2;
    }
}
