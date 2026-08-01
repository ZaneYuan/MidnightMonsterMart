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
        static void BuildProducts()
        {
            _products.Add(MakeProduct(
                "blood_orange_soda", "血橙汽水", ProductCategory.Drink, 4, 8,
                MonsterType.Vampire, true, default, false,
                new Color(0.78f, 0.13f, 0.22f), 0));

            _products.Add(MakeProduct(
                "moonlight_milk", "月光牛奶", ProductCategory.Drink, 5, 10,
                MonsterType.Werewolf, true, default, false,
                new Color(0.88f, 0.90f, 0.98f), 0));

            _products.Add(MakeProduct(
                "soul_mint", "灵魂薄荷糖", ProductCategory.Snack, 3, 7,
                MonsterType.Ghost, true, default, false,
                new Color(0.55f, 0.92f, 0.80f), 1));

            _products.Add(MakeProduct(
                "glow_jelly", "发光果冻", ProductCategory.Snack, 4, 9,
                MonsterType.Slime, true, default, false,
                new Color(0.60f, 0.95f, 0.35f), 2));

            var blackGarlic = MakeProduct(
                "black_garlic_bread", "黑蒜面包", ProductCategory.Food, 3, 6,
                default, false, MonsterType.Vampire, true,
                new Color(0.35f, 0.28f, 0.22f), 3);
            blackGarlic.isTaboo = true;
            _products.Add(blackGarlic);

            var silverChoc = MakeProduct(
                "silver_chocolate", "银纸巧克力", ProductCategory.Snack, 5, 11,
                default, false, MonsterType.Werewolf, true,
                new Color(0.72f, 0.74f, 0.80f), 1);
            silverChoc.isTaboo = true;
            _products.Add(silverChoc);

            var wardSalt = MakeProduct(
                "warding_salt", "驱灵盐", ProductCategory.Tool, 6, 14,
                default, false, MonsterType.Ghost, true,
                new Color(0.95f, 0.95f, 0.88f), 3);
            wardSalt.isTaboo = true;
            _products.Add(wardSalt);

            var cleaner = MakeProduct(
                "all_purpose_cleaner", "万能清洁剂", ProductCategory.Tool, 5, 12,
                default, false, default, false,
                new Color(0.30f, 0.70f, 0.95f), 2);
            cleaner.isCleaningTool = true;
            _products.Add(cleaner);
        }

        static ProductData MakeProduct(
            string id, string name, ProductCategory category,
            int buy, int sell,
            MonsterType preferredBy, bool hasPreference,
            MonsterType dislikedBy, bool hasDislike,
            Color tint, int shape)
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
            p.tintColor = tint;
            p.iconShape = shape;
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
            vampire.bestiaryLikes = "血橙汽水、红色饮料、高价商品";
            vampire.bestiaryDislikes = "黑蒜面包、强光、镜子";
            vampire.bestiaryRule = "靠近装饰镜时会持续掉耐心。营业前可以移走或遮住镜子，但普通顾客会因此少一点满意度。结账时可能要求换成黑色袋子。";
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
            werewolf.bestiaryLikes = "月光牛奶、大包装食品";
            werewolf.bestiaryDislikes = "银纸巧克力、高亮灯光、长时间排队";
            werewolf.bestiaryRule = "耐心掉得比谁都快。低于 20 时会撞倒附近货架，商品散落、整洁度 -20。满月夜入店即进入情绪警告。";
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
            ghost.bestiaryLikes = "灵魂薄荷糖、冷藏商品、旧物";
            ghost.bestiaryDislikes = "驱灵盐、强烈噪音、被忽视";
            ghost.bestiaryRule = "碰不到实体商品。你必须替它取货、送到灵界包装台处理后再交给它。它有时会忘记自己要买什么，需要你根据提示猜。";
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
            slime.bestiaryLikes = "发光果冻、各种饮料、包装鲜艳的商品";
            slime.bestiaryDislikes = "干燥环境、尖锐物品、被驱赶";
            slime.bestiaryRule = "移动时会留下污渍，拉低整洁度并拖慢你的移动。用万能清洁剂清理。偶尔会一口吞下两件商品，结账时你要决定怎么收费。";
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
            inspector.bestiaryLikes = "整洁的店铺、充足的库存";
            inspector.bestiaryDislikes = "缺货、污渍、错误摆放的禁忌商品";
            inspector.bestiaryRule = "第三天固定出现。穿着风衣，看不出种类。他会检查缺货、整洁度、禁忌商品摆放和顾客满意度，然后给出 A / B / C 或停业警告。";
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
                "今晚会来 4 位客人：2 个史莱姆、2 个吸血鬼。\n" +
                "① 先在左边买货 —— 建议：发光果冻 +5（史莱姆爱喝）、血橙汽水 +5（吸血鬼爱喝）、万能清洁剂 +1（擦史莱姆的污渍）。\n" +
                "② 点「开始营业」后：走到上方仓库门按 E 拿货 → 走到对应货架前<b>长按 E</b> 补货。\n" +
                "③ 顾客排队后走到收银台按 E，把商品拖到右边扫描区，再点结算。";
            d1.businessSeconds = 200f;
            d1.spawns.Add(new SpawnEntry(MonsterType.Slime, 6f));
            d1.spawns.Add(new SpawnEntry(MonsterType.Vampire, 42f));
            d1.spawns.Add(new SpawnEntry(MonsterType.Slime, 88f));
            d1.spawns.Add(new SpawnEntry(MonsterType.Vampire, 130f));
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
                "满月。狼人今晚会来，而且脾气很差。\n" +
                "月光牛奶一定要备够，银纸巧克力最好别摆在他会经过的货架上。\n" +
                "顾客开始排队了，收银慢一点整条队伍都会掉耐心。";
            d2.businessSeconds = 260f;
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
                "他检查四件事：有没有缺货、店里干不干净、禁忌商品有没有乱摆、顾客满不满意。\n" +
                "把店撑住。";
            d3.businessSeconds = 320f;
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
