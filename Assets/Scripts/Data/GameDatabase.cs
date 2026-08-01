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

        public static IReadOnlyList<ProductData> Products { get { EnsureBuilt(); return _products; } }
        public static IReadOnlyList<CustomerData> Customers { get { EnsureBuilt(); return _customers; } }
        public static IReadOnlyList<DayPlan> Days { get { EnsureBuilt(); return _days; } }

        public static int DayCount { get { EnsureBuilt(); return _days.Count; } }

        public static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            BuildProducts();
            BuildCustomers();
            BuildDays();
        }

        /// <summary>重新开始一局时调用，避免 domain reload 关闭的情况下残留旧实例。</summary>
        public static void Reset()
        {
            _products.Clear();
            _customers.Clear();
            _days.Clear();
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

        public static DayPlan GetDay(int dayNumber)
        {
            EnsureBuilt();
            for (int i = 0; i < _days.Count; i++)
                if (_days[i].dayNumber == dayNumber) return _days[i];
            return null;
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
            inspector.bestiaryRule = "第三天固定出现。穿着风衣，看不出种类。他会检查缺货、整洁度、服务事故和顾客满意度，然后给出 A / B / C 或停业警告。";
            inspector.arrivalClue = "有人递来一张空白的预约条。";
            _customers.Add(inspector);
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
            d1.title = "第一天 · 基础教学";
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
            d2.title = "第二天 · 压力增加";
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
            d3.title = "第三天 · 综合测试";
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
