using System.Collections.Generic;

namespace MonsterMart.Data
{
    /// <summary>预约条上的一行。相同内容会合并并带上数量。</summary>
    public struct NightNote
    {
        public string text;
        public int count;

        public NightNote(string text, int count)
        {
            this.text = text;
            this.count = count;
        }
    }

    /// <summary>
    /// 今晚的预约条 —— 设计文档 §2.1「查看当天可能出现的顾客类型」的推理版实现。
    ///
    /// 不直接告诉玩家「今晚来 2 个吸血鬼」，而是给出模糊线索：
    /// 「老位置，那瓶红色的。」「别让我看到银色的包装。」
    /// 玩家要自己把线索翻译成该进什么货、该<b>不</b>进什么货。
    /// 翻译工具就是怪物图鉴（Tab）—— 这样图鉴从摆设变成真正有用的东西。
    ///
    /// 生成结果按天数定种子，同一天重开拿到的是同一批线索（可复盘、可调试）。
    /// </summary>
    public static class NightNotes
    {
        /// <summary>线索是「想要」还是「忌讳」的概率分配。</summary>
        const double AvoidClueChance = 0.4;

        public static List<NightNote> Build(DayPlan plan)
        {
            var result = new List<NightNote>();
            if (plan == null || plan.spawns.Count == 0) return result;

            var rng = new System.Random(plan.dayNumber * 7919 + 13);

            var lines = new List<string>();
            for (int i = 0; i < plan.spawns.Count; i++)
            {
                var line = BuildLine(plan.spawns[i].monsterType, rng);
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }

            Shuffle(lines, rng);

            // 合并重复条目，避免同一句话刷屏
            var order = new List<string>();
            var counts = new Dictionary<string, int>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (!counts.ContainsKey(lines[i]))
                {
                    counts[lines[i]] = 0;
                    order.Add(lines[i]);
                }
                counts[lines[i]]++;
            }

            for (int i = 0; i < order.Count; i++)
                result.Add(new NightNote(order[i], counts[order[i]]));

            return result;
        }

        static string BuildLine(MonsterType type, System.Random rng)
        {
            var data = GameDatabase.GetCustomer(type);

            // 检查员不会透露自己想买什么
            if (type == MonsterType.Inspector)
                return data != null ? data.arrivalClue : null;

            var wants = WithWantClue(GameDatabase.PreferredProducts(type));
            var avoids = WithAvoidClue(GameDatabase.DislikedProducts(type));

            bool useAvoid = avoids.Count > 0 && rng.NextDouble() < AvoidClueChance;

            if (useAvoid) return avoids[rng.Next(avoids.Count)].avoidClue;
            if (wants.Count > 0) return wants[rng.Next(wants.Count)].wantClue;
            if (avoids.Count > 0) return avoids[rng.Next(avoids.Count)].avoidClue;

            return data != null ? data.arrivalClue : null;
        }

        static List<ProductData> WithWantClue(List<ProductData> products)
        {
            var result = new List<ProductData>();
            for (int i = 0; i < products.Count; i++)
                if (!string.IsNullOrEmpty(products[i].wantClue)) result.Add(products[i]);
            return result;
        }

        static List<ProductData> WithAvoidClue(List<ProductData> products)
        {
            var result = new List<ProductData>();
            for (int i = 0; i < products.Count; i++)
                if (!string.IsNullOrEmpty(products[i].avoidClue)) result.Add(products[i]);
            return result;
        }

        static void Shuffle(List<string> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
