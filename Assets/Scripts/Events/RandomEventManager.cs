using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Core;
using MonsterMart.Customers;
using MonsterMart.Data;
using MonsterMart.UI;

namespace MonsterMart.Events
{
    /// <summary>
    /// 随机事件系统 — 设计文档 §7 的五个事件：
    /// 停电 / 狼人撞倒货架 / 幽灵遗失记忆 / 史莱姆分裂 / 神秘检查员。
    /// </summary>
    public class RandomEventManager : MonoBehaviour
    {
        public bool BlackoutActive { get; private set; }

        DayPlan _plan;
        float _blackoutTimer;
        float _nextBlackoutCheck;
        bool _blackoutHappenedToday;
        bool _running;

        public void BeginDay(DayPlan plan)
        {
            _plan = plan;
            _running = true;
            BlackoutActive = false;
            _blackoutTimer = 0f;
            _blackoutHappenedToday = false;

            // 营业中段随机触发停电
            _nextBlackoutCheck = plan != null
                ? Random.Range(plan.businessSeconds * 0.25f, plan.businessSeconds * 0.65f)
                : 90f;
        }

        public void EndDay()
        {
            _running = false;
            SetBlackout(false);
            ResolvePendingInspection();
        }

        void Update()
        {
            if (!_running || Game.Manager == null || Game.Manager.State != GameState.Open) return;

            TickBlackout();
        }

        // ------------------------------------------------------------------
        // 事件一：突然停电
        // ------------------------------------------------------------------
        void TickBlackout()
        {
            if (BlackoutActive)
            {
                _blackoutTimer -= Time.deltaTime;
                if (_blackoutTimer <= 0f) RestorePower("电力自动恢复了");
                return;
            }

            if (_blackoutHappenedToday) return;
            if (_plan == null || !_plan.allowBlackout) return;

            _nextBlackoutCheck -= Time.deltaTime;
            if (_nextBlackoutCheck > 0f) return;

            TriggerBlackout();
        }

        void TriggerBlackout()
        {
            _blackoutHappenedToday = true;
            SetBlackout(true);
            _blackoutTimer = GameConfig.BlackoutDuration;

            Game.Audio?.PlayBlackout();

            // 幽灵和吸血鬼喜欢黑暗，狼人焦躁
            ApplyBlackoutMoods();

            Game.UI.ShowChoice(
                "突然停电",
                "整条街的电都断了。收银机还能用，但顾客走得更慢了。",
                new ChoiceOption("启动备用电源", $"花费 {GameConfig.BlackoutGeneratorCost} 金币，立刻恢复", () =>
                {
                    if (Game.Economy.TrySpend(GameConfig.BlackoutGeneratorCost, false))
                        RestorePower("备用电源启动，灯亮了");
                    else
                        Game.UI.Hud.Flash("钱不够启动备用电源，只能等了");
                }),
                new ChoiceOption("等待自动恢复", $"{Mathf.RoundToInt(GameConfig.BlackoutDuration)} 秒后来电", () =>
                {
                    Game.UI.Hud.Flash("你决定等电力自己恢复");
                }),
                new ChoiceOption("保持黑暗", "幽灵和吸血鬼额外满意，狼人更焦躁", () =>
                {
                    _blackoutTimer = GameConfig.BlackoutDuration * 1.6f;
                    ApplyDarknessBonus();
                    Game.UI.Hud.Flash("你把灯都关了。有些客人反而更自在了。");
                }));
        }

        void ApplyBlackoutMoods()
        {
            var all = CustomerRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null) continue;

                switch (c.Data.monsterType)
                {
                    case MonsterType.Ghost:
                    case MonsterType.Vampire:
                        c.AddSatisfaction(8f);
                        c.ApplyPatience(6f);
                        break;
                    case MonsterType.Werewolf:
                        c.ApplyPatience(-10f);
                        break;
                }
            }
        }

        void ApplyDarknessBonus()
        {
            var all = CustomerRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null) continue;

                if (c.Data.monsterType == MonsterType.Ghost || c.Data.monsterType == MonsterType.Vampire)
                {
                    c.AddSatisfaction(15f);
                    c.ApplyPatience(15f);
                }
                else if (c.Data.monsterType == MonsterType.Werewolf)
                {
                    c.ApplyPatience(-18f);
                }
            }
        }

        void RestorePower(string message)
        {
            SetBlackout(false);
            Game.UI.Hud.Flash(message);
        }

        void SetBlackout(bool active)
        {
            BlackoutActive = active;
            Game.UI?.Hud?.SetBlackout(active);
        }

        // ------------------------------------------------------------------
        // 事件二：狼人撞倒货架
        // ------------------------------------------------------------------
        /// <summary>
        /// 安保岗拦下一次货架事故的概率 — 设计文档 §4.3
        /// 「安保：处理偷窃、争吵和危险顾客」。
        /// 没排安保就是 0；排了的话概率随疲劳打折（§4.4）。
        /// </summary>
        public static float SecurityBlockChance =>
            GameConfig.SecurityBlockChance * StaffRoster.EfficiencyOn(StaffAssignment.Security);

        public void TriggerShelfCrash(CustomerController werewolf)
        {
            var store = Game.Store;
            if (store == null || werewolf == null) return;

            // 排了安保岗就有机会在狼人动手之前把它拦下来。
            // 这是「把强力员工留在店里」唯一能对抗第二天目标
            //（不让狼人撞倒超过 1 个货架）的手段。
            if (Random.value < SecurityBlockChance)
            {
                var guard = StaffRoster.FirstOn(StaffAssignment.Security);
                string name = guard != null && guard.Data != null ? guard.Data.displayName : "安保";
                Game.UI?.Hud?.Flash($"{name} 拦住了狼人，货架没倒");
                werewolf.ApplyPatience(-4f);   // 被拦下来当然不高兴
                return;
            }

            // 找离狼人最近、还没倒的货架
            Store.Shelf nearest = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < store.Shelves.Count; i++)
            {
                var shelf = store.Shelves[i];
                if (shelf.knockedOver) continue;

                float sqr = (shelf.cells.CenterWorld - werewolf.Position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = shelf;
                }
            }

            if (nearest == null) return;

            int spilled = nearest.KnockOver();

            Game.Cleanliness?.Add(-GameConfig.ShelfCrashCleanlinessCost);
            Game.Economy?.RecordSpoilage(spilled * nearest.product.purchasePrice);
            Game.Day.ShelvesKnockedOver++;
            Game.Audio?.PlayCrash();

            // 其他顾客受惊
            var all = CustomerRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i] != werewolf) all[i].ApplyPatience(-8f);

            Game.UI.Hud.Flash($"{werewolf.Data.displayName} 撞倒了「{nearest.displayName}」！走过去长按 E 扶起来");
        }

        // ------------------------------------------------------------------
        // 事件三：幽灵遗失记忆
        // ------------------------------------------------------------------
        static readonly Dictionary<string, string> AmnesiaHints = new Dictionary<string, string>
        {
            { "soul_mint",          "我生前最喜欢清凉的味道。" },
            { "blood_orange_soda",  "那是红色的，喝下去有点刺刺的。" },
            { "moonlight_milk",     "白得像满月，装在瓶子里。" },
            { "glow_jelly",         "它在黑暗里会发光，软软的。" },
            { "black_garlic_bread", "闻起来很冲，但我一直挺想试试。" },
            { "silver_chocolate",   "包装亮亮的，像镜子一样。" },
        };

        public void OpenGhostAmnesiaPuzzle(CustomerController ghost)
        {
            if (ghost == null || !ghost.AmnesiaActive) return;

            // 正确答案：它购物清单上第一件还没买到的
            ProductData answer = null;
            for (int i = 0; i < ghost.ShoppingList.Count; i++)
            {
                var p = ghost.ShoppingList[i];
                if (ghost.Basket.Contains(p)) continue;
                if (AmnesiaHints.ContainsKey(p.productId)) { answer = p; break; }
            }

            if (answer == null)
            {
                ghost.AmnesiaActive = false;
                return;
            }

            // 凑三个选项
            var options = new List<ProductData> { answer };
            int guard = 0;
            while (options.Count < 3 && guard++ < 40)
            {
                var candidate = GameDatabase.Products[Random.Range(0, GameDatabase.Products.Count)];
                if (candidate.isCleaningTool) continue;
                if (!options.Contains(candidate)) options.Add(candidate);
            }
            Shuffle(options);

            var choices = new ChoiceOption[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                var picked = options[i];
                bool correct = picked == answer;

                choices[i] = new ChoiceOption(picked.displayName, "", () =>
                {
                    ghost.AmnesiaActive = false;

                    if (correct)
                    {
                        ghost.ApplyPatience(20f);
                        ghost.AddSatisfaction(20f);
                        Game.Economy.RecordSale(6);   // 额外收入
                        Game.Reputation.Add(GameConfig.RepPerfectSpecialRequest, "帮幽灵想起了想买的东西");
                        Game.UI.Hud.Flash($"「对，就是{picked.displayName}！」");
                        Game.Audio?.PlayHappy();
                    }
                    else
                    {
                        ghost.AddSatisfaction(-20f);
                        Game.UI.Hud.Flash("「……不是这个。」幽灵失望地飘走了");
                        Game.Audio?.PlayError();
                        ghost.LeaveAngry("你猜错了");
                    }
                });
            }

            Game.UI.ShowChoice(
                $"{ghost.Data.displayName} 的记忆",
                AmnesiaHints[answer.productId] + "\n\n它到底想买什么？",
                choices);
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ------------------------------------------------------------------
        // 事件四：史莱姆分裂
        // ------------------------------------------------------------------
        public void TriggerSlimeSplit(CustomerController slime)
        {
            if (slime == null || Game.Spawner == null) return;

            Game.UI.Hud.Flash($"{slime.Data.displayName} 分裂了！小史莱姆到处乱跑");
            Game.Audio?.PlayCrash();

            for (int i = 0; i < GameConfig.SlimeSplitCount; i++)
            {
                var cell = new Vector2Int(
                    slime.Cell.x + Random.Range(-2, 3),
                    slime.Cell.y + Random.Range(-2, 3));

                cell = Game.Store.Grid.NearestWalkable(cell);
                var minion = Game.Spawner.SpawnMinion(MonsterType.Slime, cell, 0.62f);
                if (minion == null) continue;

                // 小史莱姆只会到处乱跑留污渍，不买东西；玩家可以按 E 把它们赶回去
                minion.ShoppingList.Clear();
                minion.WanderOnly = true;
                minion.WanderSeconds = 28f;
            }
        }

        // ------------------------------------------------------------------
        // 事件五：神秘检查员（第三天固定，作为原型结局）
        // ------------------------------------------------------------------
        public void RunInspection(CustomerController inspector) => RunInspection(inspector, true);

        /// <summary>
        /// <paramref name="announce"/> = false 时不弹检查结果对话框，只在 HUD 上闪一行。
        /// 营业时间到、检查员还没结账的情况走这条路 —— 那时结算界面马上就要弹出来，
        /// 再叠一个对话框会被盖住，评级本身已经写进当日结算摘要里了。
        /// </summary>
        public void RunInspection(CustomerController inspector, bool announce)
        {
            var store = Game.Store;
            var day = Game.Day;
            var clean = Game.Cleanliness;

            int score = 0;
            var lines = new List<string>();

            // 1. 是否有缺货
            int empty = store.EmptyShelfCount();
            if (empty == 0) { score += 3; lines.Add("✓ 所有货架都有货"); }
            else if (empty <= 2) { score += 1; lines.Add($"△ 有 {empty} 个货架空了"); }
            else lines.Add($"✗ 有 {empty} 个货架空着");

            // 2. 店铺是否干净
            float cleanliness = clean != null ? clean.Value : 0f;
            if (cleanliness >= 80f) { score += 3; lines.Add("✓ 店内非常整洁"); }
            else if (cleanliness >= 55f) { score += 1; lines.Add("△ 店内还算干净"); }
            else lines.Add("✗ 地上到处是污渍");

            // 3. 服务事故
            //
            // 原本这一项是「有没有乱摆禁忌商品」。改用交叉禁忌的商品表之后，
            // 几乎每件商品都是某位顾客的禁忌，「零禁忌」在有全部四种怪物的
            // 第三天根本不可能达成，这条判据就废了。
            // 改为看实际后果：有没有顾客被气走。
            int incidents = day.LeftAngry;
            if (incidents == 0) { score += 2; lines.Add("✓ 没有顾客被气走"); }
            else if (incidents <= 1) { score += 1; lines.Add("△ 有 1 位顾客生气离开"); }
            else lines.Add($"✗ 有 {incidents} 位顾客生气离开");

            // 4. 顾客满意度
            float happyRatio = day.Served > 0 ? day.Happy / (float)day.Served : 0f;
            if (day.Served >= 3 && happyRatio >= 0.7f) { score += 3; lines.Add("✓ 顾客普遍满意"); }
            else if (happyRatio >= 0.4f) { score += 1; lines.Add("△ 顾客满意度一般"); }
            else lines.Add("✗ 顾客满意度不达标");

            InspectionGrade grade =
                score >= 9 ? InspectionGrade.A :
                score >= 6 ? InspectionGrade.B :
                score >= 3 ? InspectionGrade.C : InspectionGrade.Suspended;

            day.InspectionResult = grade;
            day.InspectionDone = true;

            int repDelta =
                grade == InspectionGrade.A ? 15 :
                grade == InspectionGrade.B ? 6 :
                grade == InspectionGrade.C ? -4 : -20;

            Game.Reputation.Add(repDelta, $"检查员评价 {grade}");

            string verdict =
                grade == InspectionGrade.A ? "「这家店可以拿到午夜营业许可证。」" :
                grade == InspectionGrade.B ? "「勉强合格，下次注意。」" :
                grade == InspectionGrade.C ? "「问题不少，我会记录在案。」" :
                                             "「停业整改。」";

            if (announce)
            {
                Game.UI.ShowChoice(
                    $"检查结果：{grade}",
                    string.Join("\n", lines) + "\n\n" + verdict,
                    new ChoiceOption("我知道了", $"声望 {(repDelta >= 0 ? "+" : "")}{repDelta}", () => { }));
            }
            else
            {
                Game.UI?.Hud?.Flash($"检查员在离店前给出了评价：{grade}（声望 {(repDelta >= 0 ? "+" : "")}{repDelta}）");
            }

            Game.Audio?.PlayHappy();
        }

        /// <summary>
        /// 营业时间一到，GameManager.CloseStore() 会在同一帧就建好结算摘要，
        /// 但检查员是走到门口才触发 InspectorBehaviour.OnLeaveStore 的 —— 那要好几秒。
        /// 结果：结算界面显示「检查员评价：未完成 ✗」、声望快照少算了检查加减分，
        /// 几秒后 RunInspection 才把评级写进 Game.Day，而结局判定读的是那个新值，
        /// 于是结算界面和结局互相矛盾，第三天的目标也永远判为未达成。
        ///
        /// CloseStore() 里 EndDay() 排在 EnterSettlement() 之前，所以在这里把
        /// 还没出结果的检查先结算掉，摘要和结局就都读得到同一个评级。
        /// </summary>
        void ResolvePendingInspection()
        {
            var day = Game.Day;
            if (day == null || day.InspectionDone) return;
            if (day.CurrentPlan == null || !day.CurrentPlan.spawnInspector) return;

            var all = CustomerRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.Data == null) continue;
                if (c.Data.monsterType != MonsterType.Inspector) continue;

                RunInspection(c, false);
                return;
            }
        }

        // ------------------------------------------------------------------
        // 满月警告（狼人专属，文档 §4.2）
        // ------------------------------------------------------------------
        public void OpenFullMoonWarning(CustomerController werewolf)
        {
            if (werewolf == null) return;

            var milk = GameDatabase.GetProduct("moonlight_milk");
            var behaviour = werewolf.Behaviour as WerewolfBehaviour;

            Game.UI.Hud.Flash($"满月！{werewolf.Data.displayName} 的情绪很不稳定，20 秒内想办法");
            Game.Audio?.PlayAngry();

            Game.UI.ShowChoice(
                "满月 · 情绪警告",
                $"{werewolf.Data.displayName} 站在门口低吼。你有 20 秒。",
                new ChoiceOption("马上送上月光牛奶",
                    milk != null ? $"需要仓库或货架有 {milk.displayName}" : "",
                    () =>
                    {
                        if (TryGiveMilk(werewolf, milk))
                        {
                            behaviour?.CalmDown(werewolf);
                            Game.UI.Hud.Flash("狼人冷静下来了");
                            Game.Audio?.PlayHappy();
                        }
                        else
                        {
                            Game.UI.Hud.Flash("店里一瓶月光牛奶都没有！");
                            Game.Audio?.PlayError();
                        }
                    }),
                new ChoiceOption("关掉部分灯光", "整洁度不变，但所有顾客变慢", () =>
                {
                    behaviour?.CalmDown(werewolf);
                    SetBlackout(true);
                    _blackoutTimer = 15f;
                    Game.UI.Hud.Flash("你把一半的灯关了，狼人的呼吸平稳了一些");
                }),
                new ChoiceOption("请它离开", "避免破坏，但会掉声望", () =>
                {
                    Game.Reputation.Add(-5, "把狼人请出了店");
                    werewolf.LeaveAngry("被请出了店");
                }));
        }

        bool TryGiveMilk(CustomerController werewolf, ProductData milk)
        {
            if (milk == null) return false;

            var shelf = Game.Store.FindShelf(milk);
            if (shelf != null && shelf.Usable)
            {
                shelf.TakeOne();
                werewolf.Basket.Add(milk);
                return true;
            }

            if (Game.Store.TakeFromWarehouse(milk, 1) > 0)
            {
                werewolf.Basket.Add(milk);
                return true;
            }

            return false;
        }
    }
}
