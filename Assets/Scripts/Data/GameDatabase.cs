using System.Collections.Generic;
using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>
    /// 原型数据集。所有 ScriptableObject 在运行时构造，因此工程克隆下来
    /// 不需要任何 .asset 资产就能直接运行。
    /// 若要改为真正的资产工作流，使用编辑器菜单
    /// Tools/MonsterStore/生成数据资产 把这里的内容导出成 .asset。
    /// 商品数值完全对应设计文档 §2.2 的商品表。
    /// </summary>
    public static class GameDatabase
    {
        static bool _built;

        static readonly List<ProductData> _products = new List<ProductData>();
        static readonly List<CustomerData> _customers = new List<CustomerData>();
        static readonly List<DayPlan> _days = new List<DayPlan>();
        static readonly List<StaffData> _staff = new List<StaffData>();
        static readonly List<EnemyData> _enemies = new List<EnemyData>();
        static readonly List<RoomData> _twilightForest = new List<RoomData>();
        static readonly List<ExpeditionEventData> _expeditionEvents = new List<ExpeditionEventData>();
        static readonly List<ExpeditionBoonData> _boons = new List<ExpeditionBoonData>();

        /// <summary>默认远征队顺序 — 设计文档 §10 第一天「选择史莱姆与狼人加入远征」。</summary>
        public static readonly string[] DefaultSquad = { "slime_bobo", "werewolf_locke", "ghost_mia" };

        /// <summary>灰盒阶段的默认敌人 — 设计文档 §3.5「普通敌人：跳跳菇」。</summary>
        public const string DefaultEnemyId = "hop_mushroom";

        /// <summary>另外两种普通敌人 — §3.5「普通敌人：跳跳菇、刺藤精、森林盗贼」。</summary>
        public const string ThornSpriteId = "thorn_sprite";
        public const string ForestBanditId = "forest_bandit";

        /// <summary>精英 — §3.4「精英房：风险较高，产出稀有商品、工具或员工装备」。</summary>
        public const string EliteEnemyId = "spore_guardian";

        /// <summary>区域 Boss — §3.5「区域 Boss：孢子巨兽；击败后获得冷藏货架核心」。</summary>
        public const string BossEnemyId = "spore_behemoth";

        public static IReadOnlyList<ProductData> Products { get { EnsureBuilt(); return _products; } }
        public static IReadOnlyList<CustomerData> Customers { get { EnsureBuilt(); return _customers; } }
        public static IReadOnlyList<DayPlan> Days { get { EnsureBuilt(); return _days; } }

        public static IReadOnlyList<StaffData> Staff { get { EnsureBuilt(); return _staff; } }
        public static IReadOnlyList<EnemyData> Enemies { get { EnsureBuilt(); return _enemies; } }

        /// <summary>暮光森林的房间序列 — 设计文档 §11.1。</summary>
        public static IReadOnlyList<RoomData> TwilightForest { get { EnsureBuilt(); return _twilightForest; } }

        /// <summary>轻度肉鸽强化池 — 设计文档 §3.6。</summary>
        public static IReadOnlyList<ExpeditionBoonData> Boons { get { EnsureBuilt(); return _boons; } }

        /// <summary>三选一 —— §3.6「每次远征出现 2～3 次临时强化」。</summary>
        public const int BoonChoiceCount = 3;

        public static ExpeditionBoonData GetBoon(string id)
        {
            EnsureBuilt();
            for (int i = 0; i < _boons.Count; i++)
                if (_boons[i].boonId == id) return _boons[i];
            return null;
        }

        public static int DayCount { get { EnsureBuilt(); return _days.Count; } }

        public static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            BuildProducts();
            BuildCustomers();
            BuildDays();
            BuildStaff();
            BuildEnemies();
            BuildExpeditionEvents();
            BuildExpeditionBoons();
            BuildTwilightForest();
        }

        /// <summary>重新开始一局时调用，避免 domain reload 关闭的情况下残留旧实例。</summary>
        public static void Reset()
        {
            _products.Clear();
            _customers.Clear();
            _days.Clear();
            _staff.Clear();
            _enemies.Clear();
            _twilightForest.Clear();
            _expeditionEvents.Clear();
            _boons.Clear();
            _built = false;
        }

        public static ProductData GetProduct(string id)
        {
            EnsureBuilt();
            for (int i = 0; i < _products.Count; i++)
                if (_products[i].productId == id) return _products[i];
            return null;
        }

        public static CustomerData GetCustomer(MonsterType type)
        {
            EnsureBuilt();
            for (int i = 0; i < _customers.Count; i++)
                if (_customers[i].monsterType == type) return _customers[i];
            return null;
        }

        public static StaffData GetStaff(string id)
        {
            EnsureBuilt();
            for (int i = 0; i < _staff.Count; i++)
                if (_staff[i].staffId == id) return _staff[i];
            return null;
        }

        public static EnemyData GetEnemy(string id)
        {
            EnsureBuilt();
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i].enemyId == id) return _enemies[i];
            return null;
        }

        public static DayPlan GetDay(int dayNumber)
        {
            EnsureBuilt();
            for (int i = 0; i < _days.Count; i++)
                if (_days[i].dayNumber == dayNumber) return _days[i];
            return null;
        }

        /// <summary>
        /// 无限连续经营 —— 用户明确要求「取消三天限制」。原型只写了 3 套 DayPlan，
        /// 第 4 天开始循环复用（第 4 天 = 第 1 天的内容，以此类推）。检查员固定挂在
        /// 第 3 套 plan 上，循环之后就变成「每逢第 3 天来一次」，不用额外写判定。
        /// </summary>
        public static DayPlan GetDayCycled(int dayNumber)
        {
            EnsureBuilt();
            if (_days.Count == 0) return null;

            int wrapped = ((dayNumber - 1) % _days.Count) + 1;
            return GetDay(wrapped);
        }

        /// <summary>返回某个怪物明确喜欢的商品。</summary>
        public static List<ProductData> PreferredProducts(MonsterType type)
        {
            EnsureBuilt();
            var result = new List<ProductData>();
            for (int i = 0; i < _products.Count; i++)
                if (_products[i].IsPreferredBy(type)) result.Add(_products[i]);
            return result;
        }

        /// <summary>返回某个怪物明确讨厌的商品。</summary>
        public static List<ProductData> DislikedProducts(MonsterType type)
        {
            EnsureBuilt();
            var result = new List<ProductData>();
            for (int i = 0; i < _products.Count; i++)
                if (_products[i].IsDislikedBy(type)) result.Add(_products[i]);
            return result;
        }

        // ------------------------------------------------------------------
        // 商品 — 设计文档 §2.2
        // ------------------------------------------------------------------
        /// <summary>
        /// 商品表。核心设计：<b>几乎每件商品都是一位顾客的心头好、同时是另一位的禁忌</b>。
        /// 这样「摆不摆」永远有代价，进货就从抄作业变成下注。
        ///
        /// 　商品　　　　　喜欢　　讨厌
        /// 　血橙汽水　　　吸血鬼　—
        /// 　月光牛奶　　　狼人　　史莱姆（乳制品会让它结块）
        /// 　灵魂薄荷糖　　幽灵　　狼人（薄荷冲鼻）
        /// 　发光果冻　　　史莱姆　吸血鬼（会发光，刺眼）
        /// 　黑蒜面包　　　狼人　　吸血鬼（蒜）
        /// 　银纸巧克力　　吸血鬼　狼人（银）
        /// 　驱灵盐　　　　史莱姆　幽灵（盐）
        /// </summary>
        static void BuildProducts()
        {
            _products.Add(MakeProduct(
                "blood_orange_soda", "血橙汽水", ProductCategory.Drink, 4, 8,
                MonsterType.Vampire, true, default, false,
                new Color(0.78f, 0.13f, 0.22f), 0,
                "老位置，那瓶红色的，要冰的。",
                null));

            _products.Add(MakeProduct(
                "moonlight_milk", "月光牛奶", ProductCategory.Drink, 5, 10,
                MonsterType.Werewolf, true, MonsterType.Slime, true,
                new Color(0.88f, 0.90f, 0.98f), 0,
                "满月的日子我要喝白色的那个。",
                "别让我碰到乳白色的液体，我会结块。"));

            _products.Add(MakeProduct(
                "soul_mint", "灵魂薄荷糖", ProductCategory.Snack, 3, 7,
                MonsterType.Ghost, true, MonsterType.Werewolf, true,
                new Color(0.55f, 0.92f, 0.80f), 1,
                "我生前最喜欢清凉的味道。",
                "薄荷味太冲了，我的鼻子受不了。"));

            _products.Add(MakeProduct(
                "glow_jelly", "发光果冻", ProductCategory.Snack, 4, 9,
                MonsterType.Slime, true, MonsterType.Vampire, true,
                new Color(0.60f, 0.95f, 0.35f), 2,
                "要那个在黑暗里会发光的，软软的。",
                "把会发光的东西收起来，太刺眼了。"));

            _products.Add(MakeProduct(
                "black_garlic_bread", "黑蒜面包", ProductCategory.Food, 3, 6,
                MonsterType.Werewolf, true, MonsterType.Vampire, true,
                new Color(0.35f, 0.28f, 0.22f), 3,
                "那种闻起来很冲的面包，来两个。",
                "如果店里有蒜味，我掉头就走。"));

            _products.Add(MakeProduct(
                "silver_chocolate", "银纸巧克力", ProductCategory.Snack, 5, 11,
                MonsterType.Vampire, true, MonsterType.Werewolf, true,
                new Color(0.72f, 0.74f, 0.80f), 1,
                "包装亮亮的那款，像镜子一样。",
                "别让我看到银色的包装。"));

            _products.Add(MakeProduct(
                "warding_salt", "驱灵盐", ProductCategory.Tool, 6, 14,
                MonsterType.Slime, true, MonsterType.Ghost, true,
                new Color(0.95f, 0.95f, 0.88f), 3,
                "我需要补点矿物质，白色的颗粒。",
                "盐……请让它离我远一点。"));

            var cleaner = MakeProduct(
                "all_purpose_cleaner", "万能清洁剂", ProductCategory.Tool, 5, 12,
                default, false, default, false,
                new Color(0.30f, 0.70f, 0.95f), 2, null, null);
            cleaner.isCleaningTool = true;
            _products.Add(cleaner);
        }

        static ProductData MakeProduct(
            string id, string name, ProductCategory category,
            int buy, int sell,
            MonsterType preferredBy, bool hasPreference,
            MonsterType dislikedBy, bool hasDislike,
            Color tint, int shape,
            string wantClue, string avoidClue)
        {
            var p = ScriptableObject.CreateInstance<ProductData>();
            p.name = id;
            p.productId = id;
            p.displayName = name;
            p.category = category;
            p.purchasePrice = buy;
            p.salePrice = sell;
            p.preferredBy = preferredBy;
            p.hasPreference = hasPreference;
            p.dislikedBy = dislikedBy;
            p.hasDislike = hasDislike;
            p.isTaboo = hasDislike;     // 有人讨厌 = 摆出来就会持续惹恼那位顾客
            p.tintColor = tint;
            p.iconShape = shape;
            p.wantClue = wantClue;
            p.avoidClue = avoidClue;
            return p;
        }

        // ------------------------------------------------------------------
        // 顾客 — 设计文档 §4
        // ------------------------------------------------------------------
        static void BuildCustomers()
        {
            var vampire = ScriptableObject.CreateInstance<CustomerData>();
            vampire.name = "Vampire";
            vampire.customerId = "vampire";
            vampire.displayName = "吸血鬼";
            vampire.monsterType = MonsterType.Vampire;
            vampire.moveSpeed = 2.0f;
            vampire.maxPatience = 100f;
            vampire.patienceDecayRate = 1.3f;
            vampire.frustrationMultiplier = 2.2f;
            vampire.minBudget = 18;
            vampire.maxBudget = 40;
            vampire.minItems = 1;
            vampire.maxItems = 3;
            vampire.bodyColor = new Color(0.16f, 0.13f, 0.22f);
            vampire.accentColor = new Color(0.85f, 0.16f, 0.22f);
            vampire.silhouette = 0;
            vampire.bestiaryRule = "靠近装饰镜时会持续掉耐心。营业前可以移走或遮住镜子，但普通顾客会因此少一点满意度。结账时可能要求换成黑色袋子。";
            vampire.arrivalClue = "有位客人特别在意店里的镜子。";
            _customers.Add(vampire);

            var werewolf = ScriptableObject.CreateInstance<CustomerData>();
            werewolf.name = "Werewolf";
            werewolf.customerId = "werewolf";
            werewolf.displayName = "狼人";
            werewolf.monsterType = MonsterType.Werewolf;
            werewolf.moveSpeed = 3.1f;
            werewolf.maxPatience = 100f;
            werewolf.patienceDecayRate = 2.6f;   // §4.2「耐心下降速度比其他顾客快」
            werewolf.frustrationMultiplier = 3.0f;
            werewolf.minBudget = 16;
            werewolf.maxBudget = 34;
            werewolf.minItems = 1;
            werewolf.maxItems = 3;
            werewolf.bodyColor = new Color(0.42f, 0.30f, 0.18f);
            werewolf.accentColor = new Color(0.92f, 0.78f, 0.35f);
            werewolf.silhouette = 1;
            werewolf.bestiaryRule = "耐心掉得比谁都快。低于 20 时会撞倒附近货架，商品散落、整洁度 -20。满月夜入店即进入情绪警告。";
            werewolf.arrivalClue = "有位客人今晚情绪不太稳定。";
            _customers.Add(werewolf);

            var ghost = ScriptableObject.CreateInstance<CustomerData>();
            ghost.name = "Ghost";
            ghost.customerId = "ghost";
            ghost.displayName = "幽灵";
            ghost.monsterType = MonsterType.Ghost;
            ghost.moveSpeed = 1.7f;
            ghost.maxPatience = 100f;
            ghost.patienceDecayRate = 1.1f;
            ghost.frustrationMultiplier = 1.8f;
            ghost.minBudget = 14;
            ghost.maxBudget = 28;
            ghost.minItems = 1;
            ghost.maxItems = 2;
            ghost.bodyColor = new Color(0.72f, 0.82f, 0.92f);
            ghost.accentColor = new Color(0.45f, 0.62f, 0.85f);
            ghost.silhouette = 2;
            ghost.bestiaryRule = "碰不到实体商品。你必须替它取货、送到灵界包装台处理后再交给它。它有时会忘记自己要买什么，需要你根据提示猜。";
            ghost.arrivalClue = "有位客人说自己碰不到实体的东西。";
            _customers.Add(ghost);

            var slime = ScriptableObject.CreateInstance<CustomerData>();
            slime.name = "Slime";
            slime.customerId = "slime";
            slime.displayName = "史莱姆";
            slime.monsterType = MonsterType.Slime;
            slime.moveSpeed = 1.9f;
            slime.maxPatience = 100f;
            slime.patienceDecayRate = 1.0f;
            slime.frustrationMultiplier = 1.6f;
            slime.minBudget = 12;
            slime.maxBudget = 26;
            slime.minItems = 1;
            slime.maxItems = 3;
            slime.bodyColor = new Color(0.45f, 0.88f, 0.52f);
            slime.accentColor = new Color(0.20f, 0.55f, 0.28f);
            slime.silhouette = 3;
            slime.bestiaryRule = "移动时会留下污渍，拉低整洁度并拖慢你的移动。用万能清洁剂清理。偶尔会一口吞下两件商品，结账时你要决定怎么收费。";
            slime.arrivalClue = "有位客人走过的地方会留下水痕。";
            _customers.Add(slime);

            var inspector = ScriptableObject.CreateInstance<CustomerData>();
            inspector.name = "Inspector";
            inspector.customerId = "inspector";
            inspector.displayName = "神秘检查员";
            inspector.monsterType = MonsterType.Inspector;
            inspector.moveSpeed = 2.3f;
            inspector.maxPatience = 100f;
            inspector.patienceDecayRate = 0.7f;
            inspector.frustrationMultiplier = 1.4f;
            inspector.minBudget = 40;
            inspector.maxBudget = 40;
            inspector.minItems = 1;
            inspector.maxItems = 1;
            inspector.bodyColor = new Color(0.30f, 0.30f, 0.34f);
            inspector.accentColor = new Color(0.85f, 0.80f, 0.55f);
            inspector.silhouette = 4;
            inspector.bestiaryRule = "每逢第 3 天固定出现。穿着风衣，看不出种类。他会检查缺货、整洁度、服务事故和顾客满意度，然后给出 A / B / C 或停业警告。";
            inspector.arrivalClue = "有人递来一张空白的预约条。";
            _customers.Add(inspector);
        }

        // ------------------------------------------------------------------
        // 怪物员工 — 设计文档 §4.2
        //
        // 三项设计（远征功能 / 店内功能 / 副作用）一次性录全，
        // 远征那部分现在就用；店内与副作用等 §18 第四阶段接线。
        // ------------------------------------------------------------------
        static void BuildStaff()
        {
            var slime = MakeStaff(
                "slime_bobo", "史莱姆·啵啵", MonsterType.Slime, ElementTag.Toxic,
                health: 70f, speed: 3.2f, damage: 7f, range: 1.25f, interval: 0.8f,
                skill: "黏液爆散", skillDamage: 18f, skillRadius: 2.8f, cooldown: 5f,
                skillDesc: "向四周喷出黏液，对范围内敌人造成伤害。",
                passive: "吸收毒液、穿过缝隙；受击后短暂分裂。",
                store: "自动清理污渍并拾取掉落商品。",
                side: "使纸质包装受潮，增加少量损耗。",
                body: new Color(0.45f, 0.88f, 0.52f), accent: new Color(0.20f, 0.55f, 0.28f));
            _staff.Add(slime);

            _staff.Add(MakeStaff(
                "werewolf_locke", "狼人·洛克", MonsterType.Werewolf, ElementTag.Beast,
                health: 95f, speed: 3.9f, damage: 12f, range: 1.4f, interval: 0.7f,
                skill: "破甲爪击", skillDamage: 30f, skillRadius: 2.2f, cooldown: 7f,
                skillDesc: "一记重击，对身前敌人造成高额伤害。",
                passive: "近战破甲，追踪稀有资源和隐藏敌人。",
                store: "担任安保，降低偷窃和争吵概率。",
                side: "满月夜可能吓跑普通顾客。",
                body: new Color(0.42f, 0.30f, 0.18f), accent: new Color(0.92f, 0.78f, 0.35f)));

            _staff.Add(MakeStaff(
                "ghost_mia", "幽灵·米娅", MonsterType.Ghost, ElementTag.Spirit,
                health: 55f, speed: 3.5f, damage: 9f, range: 2.2f, interval: 1.0f,
                skill: "灵魂震荡", skillDamage: 20f, skillRadius: 3.2f, cooldown: 6f,
                skillDesc: "以自身为中心释放灵波，范围很大。",
                passive: "穿过部分墙体，发现隐藏房间与捷径。",
                store: "穿越拥堵顾客补货，效率很高。",
                side: "让商品悬浮，可能惊吓人类顾客。",
                body: new Color(0.72f, 0.82f, 0.92f), accent: new Color(0.45f, 0.62f, 0.85f)));

            var vera = MakeStaff(
                "vampire_vera", "吸血鬼·维拉", MonsterType.Vampire, ElementTag.Blood,
                health: 75f, speed: 3.4f, damage: 11f, range: 1.5f, interval: 0.85f,
                skill: "血之收割", skillDamage: 24f, skillRadius: 2.6f, cooldown: 6.5f,
                skillDesc: "对范围内敌人造成伤害，对精英额外有效。",
                passive: "对精英怪额外伤害，低生命时吸血。",
                store: "提高深夜顾客消费和高价品销售。",
                side: "可能偷喝一瓶血橙汽水。",
                body: new Color(0.16f, 0.13f, 0.22f), accent: new Color(0.85f, 0.16f, 0.22f));
            // §4.2 的「对精英怪额外伤害」在这里落成数值：
            // 带上维拉，精英房和 Boss 房会明显轻松，代价是她不在店里管高价品销售。
            vera.eliteDamageMultiplier = 1.5f;
            _staff.Add(vera);
        }

        static StaffData MakeStaff(
            string id, string name, MonsterType type, ElementTag element,
            float health, float speed, float damage, float range, float interval,
            string skill, float skillDamage, float skillRadius, float cooldown, string skillDesc,
            string passive, string store, string side, Color body, Color accent)
        {
            var s = ScriptableObject.CreateInstance<StaffData>();
            s.name = id;
            s.staffId = id;
            s.displayName = name;
            s.monsterType = type;
            s.element = element;

            s.maxHealth = health;
            s.moveSpeed = speed;
            s.attackDamage = damage;
            s.attackRange = range;
            s.attackInterval = interval;

            s.skillName = skill;
            s.skillDamage = skillDamage;
            s.skillRadius = skillRadius;
            s.skillCooldown = cooldown;
            s.skillDescription = skillDesc;

            s.expeditionPassive = passive;
            s.storeAbility = store;
            s.sideEffect = side;

            s.bodyColor = body;
            s.accentColor = accent;
            return s;
        }

        // ------------------------------------------------------------------
        // 远征敌人 — 设计文档 §3.5「暮光森林」
        //
        // §1.5 原型规模写的是「3 种普通敌人 + 1 个区域 Boss」，
        // §3.5 点名普通敌人是跳跳菇、刺藤精、森林盗贼，区域 Boss 是孢子巨兽。
        // 精英没有点名，按 §3.4「精英房：风险较高，产出稀有商品」补一只孢囊守卫，
        // 顺带把玩家引向 Boss 的孢子主题。
        // ------------------------------------------------------------------
        static void BuildEnemies()
        {
            var hop = MakeEnemy(
                DefaultEnemyId, "跳跳菇", EnemyTier.Normal,
                health: 45f, speed: 1.8f, aggro: 7f,
                damage: 9f, range: 1.2f, interval: 1.7f, telegraph: 0.6f,
                // §22 最小可行版本要验证的正是这条链路：打怪掉血橙汽水 → 带回店里卖给吸血鬼
                lootId: "blood_orange_soda", lootMin: 2, lootMax: 3,
                body: new Color(0.78f, 0.62f, 0.85f),
                accent: new Color(0.42f, 0.28f, 0.50f));
            hop.silhouette = 6;    // 蘑菇外形
            hop.xpReward = 10f;
            _enemies.Add(hop);

            // 毒雾区的常驻居民：血少但打得快，逼小队分散走位
            var thornSprite = MakeEnemy(
                ThornSpriteId, "刺藤精", EnemyTier.Normal,
                health: 34f, speed: 2.4f, aggro: 8f,
                damage: 7f, range: 1.1f, interval: 1.2f, telegraph: 0.45f,
                lootId: "glow_jelly", lootMin: 1, lootMax: 2,
                body: new Color(0.42f, 0.72f, 0.38f),
                accent: new Color(0.20f, 0.42f, 0.22f));
            thornSprite.silhouette = 7;    // 荆棘外形
            thornSprite.xpReward = 10f;
            _enemies.Add(thornSprite);

            // 盯着战利品的机会主义者：血厚一点、打一下很痛，前摇也最长
            var forestBandit = MakeEnemy(
                ForestBanditId, "森林盗贼", EnemyTier.Normal,
                health: 58f, speed: 2.1f, aggro: 9f,
                damage: 13f, range: 1.35f, interval: 2.0f, telegraph: 0.75f,
                lootId: "silver_chocolate", lootMin: 1, lootMax: 2,
                body: new Color(0.55f, 0.42f, 0.30f),
                accent: new Color(0.90f, 0.72f, 0.30f));
            forestBandit.silhouette = 8;   // 戴眼罩的盗贼外形
            forestBandit.xpReward = 12f;
            _enemies.Add(forestBandit);

            // ---- 精英（§3.4「精英房：风险较高，产出稀有商品、工具或员工装备」）----
            //
            // 玩法本体是那层护甲：普通攻击只打进一半，技能打满。
            // 小队的自动普攻磨不动它，玩家必须挑时机按 1~3
            // （§3.3「玩家负责走位、躲避预警区和主动技能时机」）。
            var guardian = MakeEnemy(
                EliteEnemyId, "孢囊守卫", EnemyTier.Elite,
                health: 150f, speed: 1.6f, aggro: 9f,
                damage: 18f, range: 1.5f, interval: 2.1f, telegraph: 0.85f,
                lootId: "warding_salt", lootMin: 3, lootMax: 4,
                body: new Color(0.68f, 0.55f, 0.82f),
                accent: new Color(0.95f, 0.80f, 0.35f));
            guardian.basicAttackResist = 0.5f;
            guardian.bodyScale = 1.35f;
            guardian.silhouette = 9;   // 带孢子荚的守卫外形
            guardian.xpReward = 25f;
            guardian.coinMin = 8;
            guardian.coinMax = 14;
            _enemies.Add(guardian);

            // ---- 区域 Boss（§3.5「区域 Boss：孢子巨兽；击败后获得冷藏货架核心」）----
            //
            // §3.3 要求 Boss「通过区域机制、护送商品或关闭装置制造变化」，
            // 这里选的是关闭装置：三个孢子喷口，开着时巨兽几乎无敌并持续灼伤小队，
            // 队长跑过去按 E 一个个关掉，全关后才有一段破防窗口。
            var behemoth = MakeEnemy(
                BossEnemyId, "孢子巨兽", EnemyTier.Boss,
                health: 420f, speed: 1.3f, aggro: 12f,
                damage: 26f, range: 2.0f, interval: 2.6f, telegraph: 1.1f,
                lootId: "moonlight_milk", lootMin: 4, lootMax: 6,
                body: new Color(0.52f, 0.62f, 0.35f),
                accent: new Color(0.85f, 0.95f, 0.45f));
            behemoth.bodyScale = 2.0f;
            behemoth.ventCount = 3;
            behemoth.ventReopenSeconds = 14f;
            behemoth.ventPulseSeconds = 2.4f;
            behemoth.ventPulseDamage = 7f;
            behemoth.ventPulseRadius = 2.4f;
            behemoth.shieldedDamageMultiplier = 0.15f;
            behemoth.coinMin = 25;
            behemoth.coinMax = 40;
            behemoth.coldShelfCores = 1;
            // §2.1 阶段六「解锁灰烬火山、幽灵旧城等新区域」
            behemoth.unlocksRegionId = "ash_volcano";
            behemoth.unlocksRegionName = "灰烬火山";
            behemoth.silhouette = 10;   // 带结节的巨兽外形
            behemoth.xpReward = 60f;
            _enemies.Add(behemoth);
        }

        static EnemyData MakeEnemy(
            string id, string name, EnemyTier tier,
            float health, float speed, float aggro,
            float damage, float range, float interval, float telegraph,
            string lootId, int lootMin, int lootMax,
            Color body, Color accent)
        {
            var e = ScriptableObject.CreateInstance<EnemyData>();
            e.name = id;
            e.enemyId = id;
            e.displayName = name;
            e.tier = tier;

            e.maxHealth = health;
            e.moveSpeed = speed;
            e.aggroRadius = aggro;

            e.attackDamage = damage;
            e.attackRange = range;
            e.attackInterval = interval;
            e.telegraphSeconds = telegraph;

            e.lootProductId = lootId;
            e.lootMin = lootMin;
            e.lootMax = lootMax;

            e.bodyColor = body;
            e.accentColor = accent;
            return e;
        }

        // ------------------------------------------------------------------
        // 暮光森林 — 设计文档 §11.1 的六个区域。
        //
        // §18 第二阶段说「5 个房间」，指的是营地之后那 5 个：
        // 资源小径 / 毒雾区 / 隐藏树洞 / 精英空地 / Boss 空地。
        // 营地本身不算，它只是出发点和撤退传送点所在地。
        //
        // ------------------------------------------------------------------
        static void BuildTwilightForest()
        {
            _twilightForest.Add(MakeRoom(
                "camp", "入口营地", RoomKind.Camp, null, 0,
                "查看目标、队伍和撤退规则。踩上传送点出发。"));

            // §3.5 暮光森林主要商品：血橙、月光蘑菇、狼莓、精灵汽水、发光果冻
            var resource = MakeRoom(
                "resource_path", "资源小径", RoomKind.Resource, DefaultEnemyId, 1,
                "基础采集与零星普通敌人。走到发光的采集点按 E 收货，注意携带容量。");
            resource.harvestProductIds.Add("blood_orange_soda");
            resource.harvestProductIds.Add("glow_jelly");
            resource.harvestProductIds.Add("moonlight_milk");
            resource.harvestPerNode = 3;
            _twilightForest.Add(resource);

            // §3.6「每次远征出现 2～3 次临时强化」：整条路线上打两次勾 ——
            // 一次在真正开打之前，一次在进精英空地之前，都卡在压力抬升的节点上。
            var mist = MakeRoom(
                "poison_mist", "毒雾区", RoomKind.Battle, ThornSpriteId, 3,
                "毒雾弥漫，刺藤精成群。它们血薄但出手快，别让全队挤在一处。");
            mist.offersBoon = true;
            _twilightForest.Add(mist);

            var hollow = MakeRoom(
                "hidden_hollow", "隐藏树洞", RoomKind.Event, null, 0,
                "树洞里似乎有别的东西。");
            hollow.eventId = "hollow_trader";
            _twilightForest.Add(hollow);

            // 精英空地：一只带甲的孢囊守卫 + 两名森林盗贼。
            // 普通攻击对守卫只打进一半，先清杂兵还是先集火守卫是这间房的取舍。
            var elite = MakeRoom(
                "elite_clearing", "精英空地", RoomKind.Elite, EliteEnemyId, 1,
                "孢囊守卫披着厚甲，普通攻击只打进一半 —— 攒好技能再上（1~3）。" +
                "旁边还有两名森林盗贼盯着你的背包。");
            elite.minionEnemyId = ForestBanditId;
            elite.minionCount = 2;
            elite.offersBoon = true;
            elite.harvestProductIds.Add("warding_salt");
            elite.harvestPerNode = 4;
            _twilightForest.Add(elite);

            _twilightForest.Add(MakeRoom(
                "boss_clearing", "Boss 空地", RoomKind.Boss, BossEnemyId, 1,
                "孢子巨兽盘踞于此，三个孢子喷口撑着它的护盾并不断灼伤四周。" +
                "走到喷口上按 E 一个个关掉，全关之后才打得动它。"));
        }

        // ------------------------------------------------------------------
        // 事件房 — 设计文档 §3.4
        // ------------------------------------------------------------------
        static void BuildExpeditionEvents()
        {
            var trader = ScriptableObject.CreateInstance<ExpeditionEventData>();
            trader.name = "hollow_trader";
            trader.eventId = "hollow_trader";
            trader.title = "树洞里的行商";
            trader.body =
                "树洞深处坐着一个裹着斗篷的家伙，面前摆了几箱货。\n" +
                "「买一点吗？还是……你想自己动手拿？」";

            trader.options.Add(new ExpeditionEventOption(
                "买下那箱银纸巧克力", "花 24 金币，换 6 件",
                ExpeditionEventEffect.Trade)
            {
                coinCost = 24,
                productId = "silver_chocolate",
                productCount = 6,
            });

            trader.options.Add(new ExpeditionEventOption(
                "趁它不注意搬走", "白拿 4 件，但全队各掉 12 点生命",
                ExpeditionEventEffect.Scavenge)
            {
                productId = "silver_chocolate",
                productCount = 4,
                squadDamage = 12f,
            });

            trader.options.Add(new ExpeditionEventOption(
                "不做交易，继续赶路", "什么都不发生",
                ExpeditionEventEffect.Leave));

            _expeditionEvents.Add(trader);
        }

        // ------------------------------------------------------------------
        // 轻度肉鸽三选一 — 设计文档 §3.6
        //
        // 文档直接给了这四个示例，这里一一落成数值。核心约束是那句
        // 「强化优先提供收益与代价，而不是无脑增加伤害」——
        // 所以每一条都是「拿到什么 / 付出什么」成对出现。
        // ------------------------------------------------------------------
        static void BuildExpeditionBoons()
        {
            var insurance = MakeBoon(
                "fragile_insurance", "易碎品保险",
                "被击退时战利品几乎不会损坏（保留率 50% → 90%）",
                "队长背着保险箱，移动速度 -20%");
            insurance.failKeepRatioBonus = 0.4f;
            insurance.captainSpeedMultiplier = 0.8f;
            _boons.Add(insurance);

            var wholesale = MakeBoon(
                "wholesale_contract", "批发契约",
                "普通敌人的商品掉落翻倍",
                "Boss 奖励品质下降，掉落减半");
            wholesale.normalLootMultiplier = 2f;
            wholesale.bossLootMultiplier = 0.5f;
            _boons.Add(wholesale);

            var overtime = MakeBoon(
                "overtime_frenzy", "加班狂热",
                "全队技能冷却 -35%",
                "每次放技能都要透支自己，施法者掉 8 点生命");
            overtime.skillCooldownMultiplier = 0.65f;
            overtime.skillSelfDamage = 8f;
            _boons.Add(overtime);

            var delivery = MakeBoon(
                "slime_delivery", "史莱姆快递",
                "拾取范围扩大到 2.2 倍，走过路过就能收货",
                "史莱姆忙着运货，攻击力 -30%");
            delivery.pickupRadiusMultiplier = 2.2f;
            delivery.slimeAttackMultiplier = 0.7f;
            _boons.Add(delivery);
        }

        static ExpeditionBoonData MakeBoon(string id, string name, string benefit, string cost)
        {
            var b = ScriptableObject.CreateInstance<ExpeditionBoonData>();
            b.name = id;
            b.boonId = id;
            b.displayName = name;
            b.benefit = benefit;
            b.cost = cost;
            return b;
        }

        public static ExpeditionEventData GetExpeditionEvent(string id)
        {
            EnsureBuilt();
            for (int i = 0; i < _expeditionEvents.Count; i++)
                if (_expeditionEvents[i].eventId == id) return _expeditionEvents[i];
            return null;
        }

        static RoomData MakeRoom(string id, string name, RoomKind kind,
                                 string enemyId, int enemyCount, string briefing)
        {
            var r = ScriptableObject.CreateInstance<RoomData>();
            r.name = id;
            r.roomId = id;
            r.displayName = name;
            r.kind = kind;
            r.enemyId = enemyId;
            r.enemyCount = enemyCount;
            r.briefing = briefing;
            return r;
        }

        // ------------------------------------------------------------------
        // 三天流程 — 设计文档 §8
        // ------------------------------------------------------------------
        static void BuildDays()
        {
            // 第一天：基础教学
            var d1 = ScriptableObject.CreateInstance<DayPlan>();
            d1.name = "Day1";
            d1.dayNumber = 1;
            // 不带「第 N 天」前缀 —— 循环复用到第 4/7/10……天时，PreparationView
            // 会自己拼上当时的真实天数（见 GameDatabase.GetDayCycled）。
            d1.title = "基础教学";
            d1.briefing =
                "右边是今晚收到的预约条 —— 客人不会报自己是谁，只留下一句话。\n" +
                "对着商品列表想想他们要什么，把货备上。<b>猜错了就是白进货 + 客人空手离开。</b>\n" +
                "注意：几乎每件商品都有人爱、有人忌 —— 摆上货架赚一个人的钱，就可能得罪另一个。\n" +
                "买完点「一键摆货」铺到货架上，再点「开始营业」。别忘了留一瓶万能清洁剂擦地。";
            d1.businessSeconds = 200f;
            d1.maxItemsPerCustomer = 1;   // 教学日：一人只买一件
            d1.spawns.Add(new SpawnEntry(MonsterType.Slime, 12f));
            d1.spawns.Add(new SpawnEntry(MonsterType.Vampire, 52f));
            d1.spawns.Add(new SpawnEntry(MonsterType.Slime, 100f));
            d1.spawns.Add(new SpawnEntry(MonsterType.Vampire, 142f));
            d1.goalCustomersServed = 4;
            d1.goalMinProfit = 1;
            d1.goalDescription = "完成 4 名顾客结账，且当日利润大于 0。";
            d1.allowSlimeSplit = false;
            _days.Add(d1);

            // 第二天：压力增加
            var d2 = ScriptableObject.CreateInstance<DayPlan>();
            d2.name = "Day2";
            d2.dayNumber = 2;
            d2.title = "压力增加";
            d2.briefing =
                "满月。今晚的预约条里有两位新面孔 —— 图鉴里还没有它们的记录，只能靠商品名硬猜。\n" +
                "人多了，喜好开始互相打架：同一件商品可能是一位客人的心头好、另一位的禁区。\n" +
                "摆之前先想清楚今晚谁会来。顾客开始排队了，收银慢一点整条队伍都会掉耐心。";
            d2.businessSeconds = 260f;
            d2.maxItemsPerCustomer = 2;
            d2.spawns.Add(new SpawnEntry(MonsterType.Slime, 5f));
            d2.spawns.Add(new SpawnEntry(MonsterType.Vampire, 28f));
            d2.spawns.Add(new SpawnEntry(MonsterType.Werewolf, 55f));
            d2.spawns.Add(new SpawnEntry(MonsterType.Slime, 82f));
            d2.spawns.Add(new SpawnEntry(MonsterType.Ghost, 110f));
            d2.spawns.Add(new SpawnEntry(MonsterType.Vampire, 140f));
            d2.spawns.Add(new SpawnEntry(MonsterType.Werewolf, 175f));
            d2.goalCustomersServed = 5;
            d2.goalMinReputation = 40;
            d2.goalMaxShelvesKnocked = 1;   // §10 第二天「不让狼人破坏超过一个货架」
            d2.goalDescription = "至少服务 5 名顾客，声望达到 40，且不让狼人撞倒超过 1 个货架。";
            d2.allowBlackout = true;
            d2.allowShelfCrash = true;
            d2.allowGhostAmnesia = true;
            d2.fullMoon = true;
            _days.Add(d2);

            // 第三天：综合测试
            var d3 = ScriptableObject.CreateInstance<DayPlan>();
            d3.name = "Day3";
            d3.dayNumber = 3;
            d3.title = "综合测试";
            d3.briefing =
                "午夜商业管理局今晚会派检查员来。他穿着风衣，混在顾客里，你分不出是谁。\n" +
                "他检查四件事：有没有缺货、店里干不干净、有没有顾客被气走、顾客满不满意。\n" +
                "四种怪物今晚全会到场 —— 每件商品都会同时讨好一个人、得罪另一个人。撑住。";
            d3.businessSeconds = 320f;
            d3.maxItemsPerCustomer = 3;
            d3.spawns.Add(new SpawnEntry(MonsterType.Slime, 4f));
            d3.spawns.Add(new SpawnEntry(MonsterType.Werewolf, 26f));
            d3.spawns.Add(new SpawnEntry(MonsterType.Ghost, 50f));
            d3.spawns.Add(new SpawnEntry(MonsterType.Vampire, 72f));
            d3.spawns.Add(new SpawnEntry(MonsterType.Inspector, 96f));
            d3.spawns.Add(new SpawnEntry(MonsterType.Slime, 124f));
            d3.spawns.Add(new SpawnEntry(MonsterType.Werewolf, 152f));
            d3.spawns.Add(new SpawnEntry(MonsterType.Vampire, 186f));
            d3.spawns.Add(new SpawnEntry(MonsterType.Ghost, 218f));
            d3.goalMinReputation = 60;
            d3.goalMinCleanliness = 60;
            d3.goalDescription = "整洁度保持在 60 以上，声望达到 60，并完成检查员事件。";
            d3.allowBlackout = true;
            d3.allowShelfCrash = true;
            d3.allowGhostAmnesia = true;
            d3.allowSlimeSplit = true;
            d3.spawnInspector = true;
            _days.Add(d3);
        }
    }
}
