using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MonsterMart.Combat;
using MonsterMart.Core;
using MonsterMart.Customers;
using MonsterMart.Data;
using MonsterMart.Events;
using MonsterMart.Expeditions;
using MonsterMart.Player;
using MonsterMart.Staff;
using MonsterMart.Store;
using MonsterMart.UI;

namespace MonsterMart.EditorTools
{
    /// <summary>
    /// 无头冒烟测试。工程里没有装 com.unity.test-framework，也没有 asmdef，
    /// 所以这里不用 NUnit，直接做成一个可以用 -executeMethod 调起、
    /// 用退出码表示成败的编辑器入口：
    ///
    ///   Unity.exe -batchmode -nographics -projectPath &lt;proj&gt; \
    ///             -executeMethod MonsterMart.EditorTools.SmokeTests.RunAll
    ///
    /// 退出码 0 = 全部通过，1 = 有用例失败。
    ///
    /// 覆盖的是「三天流程」上那些不跑完整局就发现不了、
    /// 但一旦回归就会让第三天判定错掉的地方。
    /// </summary>
    public static class SmokeTests
    {
        static int _passed;
        static readonly List<string> _failures = new List<string>();

        [MenuItem("Tools/MonsterStore/运行冒烟测试", false, 20)]
        public static void RunFromMenu()
        {
            int failed = Run();
            Debug.Log(failed == 0
                ? $"[SmokeTests] 全部通过（{_passed}）"
                : $"[SmokeTests] {failed} 个用例失败");
        }

        /// <summary>批处理入口。</summary>
        public static void RunAll()
        {
            int failed = Run();
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        static int Run()
        {
            _passed = 0;
            _failures.Clear();

            Case("数据集：三天流程齐全", Test_DatabaseHasThreeDays);
            Case("店铺：所有货架都绑定到了真实商品", Test_EveryShelfHasProduct);
            Case("第三天：营业结束时检查员还没结账 → 结算前必须出评级", Test_PendingInspectionResolvedBeforeSettlement);
            Case("第三天：重复调用 EndDay 不能重复结算检查", Test_InspectionIsNotAppliedTwice);
            Case("第一天：没有检查员的日子不能凭空判出评级", Test_NoInspectionOnDaysWithoutInspector);
            Case("生气离店：一个顾客只能被结算一次", Test_AngryCustomerIsCountedOnlyOnce);
            Case("生气离店：不能把账记到第二天头上", Test_AngryCustomerDoesNotBleedIntoNextDay);
            Case("存档：累计利润能存下来并在读档后恢复", Test_TotalProfitSurvivesSaveLoad);
            Case("存档：没有 totalProfit 的旧存档照样能读，默认 0", Test_LegacySaveWithoutTotalProfitStillLoads);
            Case("终局：通关后重进要从第一天开新局，不能重打最后一天", Test_FinishedRunStartsFreshOnReboot);
            Case("终局：没打完的存档（含旧存档）照常续玩", Test_UnfinishedRunStillResumes);
            Case("结算：普通日结算后退出重进要接着打下一天，利润不重复累计", Test_SettledDayResumesAtNextDay);
            Case("循环：三天结算后不弹结局，DayPlan 循环复用、检查员照旧每 3 天来一次", Test_DayLoopContinuesPastDayThreeWithNoForcedEnding);
            Case("重新开始：丢弃本局进度但保留图鉴等跨局数据", Test_ManualRestartDiscardsRunButKeepsCrossRunData);
            Case("暂停：收银会话冻住，不推时间、不掉耐心、不扣声望、不算愤怒离店", Test_CheckoutSessionFrozenWhilePaused);
            Case("收银：点一下就扫描，扫描间隔真的在拦人，重复扫描有处罚", Test_CheckoutScanIsClickBasedNotPositional);
            Case("收银：顾客还没到位时按 E 也有反馈，不是彻底没反应", Test_CheckoutGivesFeedbackWhenCustomerNotYetAtCounter);
            Case("收银：顾客卡在排队半路走不到时，卡够久会自动吸附到排队点", Test_StuckQueueCustomerSnapsToSlot);
            Case("货架：满了时手上正拿着同款商品也有反馈，不是彻底没反应", Test_FullShelfGivesFeedbackInsteadOfSilence);
            Case("仓库：能把手上不要的商品放回仓库，不用被迫换成别的", Test_StockRoomCanPutBackCarriedItem);
            Case("营业：倍速只在营业中生效，且不会带出营业阶段", Test_BusinessSpeedAppliesDuringOpenOnly);
            Case("营业：倍速是跨局偏好，存能读、旧存档不会炸", Test_BusinessSpeedSurvivesSaveLoad);
            Case("目标：第二天的「撞倒货架」目标必须被判定", Test_ShelfCrashGoalIsEvaluated);
            Case("存档：仓库与货架库存能存下来并恢复", Test_StockSurvivesSaveLoad);
            Case("远征：员工与敌人数据齐全（§4.1 三项设计）", Test_ExpeditionDataIsComplete);
            Case("远征：灰盒房间可走，营地与刷怪点都在可行走区", Test_ExpeditionRoomIsWalkable);
            Case("远征：击败敌人掉商品，带回后进仓库（§18 第一阶段完成标准）", Test_ExpeditionLootReachesWarehouse);
            Case("远征：小队被击退只保留一半战利品（§3.7）", Test_FailedExpeditionLosesHalf);
            Case("小队：三人上阵且队形互不重叠（§3.3）", Test_SquadOfThreeHoldsFormation);
            Case("战斗：出手动作走完才扣血，不是冷却好瞬间结算", Test_StaffAttackDealsDamageOnlyAfterWindupCompletes);
            Case("战斗：出手过程中目标死了，这一下打空而不是报错/补刀", Test_StaffAttackWhiffsIfTargetDiesDuringWindup);
            Case("信息面板：MP 是冷却的展示包装，冷却好=满、放完技能=空", Test_ManaPercentReflectsSkillCooldown);
            Case("小队：目标标记轮换、死亡与收队后自动清除（§3.2）", Test_TargetMarkingCyclesAndClears);
            Case("小队：卡住的队友会自动归队（§18 第二阶段完成标准）", Test_StuckFollowerReturnsToCaptain);
            Case("房间：暮光森林路线覆盖 §3.4 六类房间，营地后 5 间", Test_TwilightForestRouteShape);
            Case("房间：没清场不放行，清场后才能进下一间（§3.4 传送点）", Test_ExitPortalGatesOnClear);
            Case("房间：切换后小队沿用同一批人、敌人与掉落按房间重建", Test_RoomSwitchKeepsSquadRebuildsRoom);
            Case("房间：走完最后一间即收队结算", Test_LastRoomFinishesExpedition);
            Case("采集：资源房有采集点，采到的货进背包（§3.2/§3.4）", Test_HarvestNodeFillsBag);
            Case("采集：采集点之间不重叠（刷新点带随机，连开 12 趟都不许挨着）", Test_HarvestNodesNeverOverlap);
            Case("采集：携带容量封顶，装不下的留在原地（§3.2 携带容量）", Test_BagCapacityLimitsHarvest);
            Case("事件房：交易扣钱给货，钱不够则不生效（§3.4）", Test_EventTradeRequiresCoins);
            Case("事件房：搜刮白拿但全队掉血（收益与代价）", Test_EventScavengeCostsHealth);
            Case("敌人：3 种普通 + 1 精英 + 1 区域 Boss，分级数据齐全（§1.5/§3.5）", Test_EnemyRosterCoversAllTiers);
            Case("精英：护甲只挡普通攻击，技能打满（§3.4）", Test_EliteArmorOnlyBlocksBasicAttacks);
            Case("精英房：精英本体与杂兵都按房间数据刷出（§3.4）", Test_EliteRoomSpawnsGuardianAndMinions);
            Case("维拉：对精英与 Boss 额外伤害，对普通敌人没有加成（§4.2）", Test_VeraHitsElitesHarder);
            Case("队长技能：冷却好能放，秒杀范围内敌人，放完立刻进冷却", Test_CaptainSkillOneShotsNonBossAndHasCooldown);
            Case("队长技能：不能伤到 Boss，Boss 战还是得靠关喷口", Test_CaptainSkillSparesBoss);
            Case("Boss：喷口开着几乎打不动，全关才破防（§3.3 关闭装置）", Test_BossShieldHoldsUntilVentsClosed);
            Case("Boss：开着的喷口会持续灼伤范围内的小队（区域机制的代价）", Test_OpenVentsBurnNearbySquad);
            Case("Boss：破防是窗口不是买断，喷口会重新喷发（§3.3）", Test_VentsReopenAfterWindow);
            Case("Boss：击败后掉冷藏货架核心并解锁下一地区（§3.4/§3.5）", Test_BossDropsColdShelfCoreAndUnlocksRegion);
            Case("Boss：冷藏货架核心能存档、开新局要清掉", Test_ColdShelfCoreSurvivesSaveButNotNewRun);
            Case("强化：每一条都收益与代价成对，不是无脑加伤害（§3.6）", Test_EveryBoonPairsBenefitWithCost);
            Case("强化：一趟远征给 2~3 次三选一，候选互不重复（§3.6）", Test_BoonOffersAreTwoToThreeAndDistinct);
            Case("强化：仅本次远征生效，下一趟从零开始（§3.6）", Test_BoonsExpireWithTheExpedition);
            Case("强化「批发契约」：普通掉落翻倍，Boss 奖励缩水", Test_WholesaleContractTradesBossLootForVolume);
            Case("强化「加班狂热」：冷却缩短但每次施法自损", Test_OvertimeFrenzyTradesHealthForCooldown);
            Case("强化「史莱姆快递」：拾取范围变大但史莱姆变弱", Test_SlimeDeliveryTradesDamageForReach);
            Case("强化「易碎品保险」：被击退保留更多但队长变慢", Test_FragileInsuranceTradesSpeedForLoot);
            Case("循环：一天从晨会走到结算，再进下一天的晨会（§2.1）", Test_DayLoopRunsMorningToNextMorning);
            Case("循环：一天只有一趟远征，去过或选了不出门都不能再去（§2.1）", Test_OnlyOneExpeditionPerDay);
            Case("纯远征模式：跳过日夜循环，没排班也能打，打完能立刻再来一趟", Test_ExpeditionOnlyModeLoopsWithoutDayCycle);
            Case("排班：远征队最多 3 人，满员再加会被拒（§3.3）", Test_SquadCapIsEnforced);
            Case("排班：出征 + 夜班要吃两份疲劳，全休才回血（§4.4）", Test_DoubleShiftCostsMoreFatigue);
            Case("排班：疲劳越高效率越低，但不会归零（§4.4）", Test_FatigueLowersEfficiencyWithFloor);
            Case("岗位·收银：排了人扫描更宽、排队更耐烦，没排回基线（§4.3）", Test_CashierOnDutyEasesCheckout);
            Case("岗位·补货：营业中自动从仓库往货架搬，没排人不搬（§4.3）", Test_RestockerRefillsShelvesDuringBusiness);
            Case("岗位·安保：排了才有概率拦下货架事故（§4.3）", Test_SecurityOnlyBlocksWhenStaffed);
            Case("打怪升级：经验攒够就升级，等级带来伤害与生命加成", Test_StaffLevelsUpFromXpAndBuffsCombat);
            Case("队长升级：经验攒够就升级，升级扩大背包容量", Test_CaptainLevelsUpAndExpandsBagCapacity);
            Case("队长升级：等级与经验能存能读，旧存档退回 1 级、开新局清空", Test_CaptainLevelSurvivesSaveButNotNewRun);
            Case("打怪升级：击杀经验按存活小队均分", Test_ExpeditionKillsAwardXpToSquad);
            Case("远征：击败敌人直接掉金币，独立于商品掉落", Test_ExpeditionKillsDropCoins);
            Case("存档：排班与疲劳能存能读，旧存档退回默认、开新局清空", Test_RosterSurvivesSaveButNotNewRun);
            Case("打怪升级：等级与经验能存能读，旧存档退回 1 级、开新局清空", Test_StaffLevelSurvivesSaveButNotNewRun);
            Case("存档：远征跑完再退出，重进落回闭店准备而不是再跑一趟", Test_FinishedExpeditionSurvivesReload);
            Case("角色外观：员工与远征敌人都有专属贴图，不再是一个圆点", Test_CharacterArtCoversStaffAndEnemies);
            Case("界面：整套 UI 能搭起来，晨会/备货/结算/结局都能打开并刷新", Test_AllPanelsBuildAndOpen);
            Case("营业：进货界面能随时打开补货，且不会误触发重复开门", Test_PreparationViewOpensDuringBusiness);

            Debug.Log($"[SmokeTests] 通过 {_passed} / 失败 {_failures.Count}");
            for (int i = 0; i < _failures.Count; i++)
                Debug.LogError($"[SmokeTests] 失败：{_failures[i]}");

            return _failures.Count;
        }

        static void Case(string name, Action body)
        {
            GameObject sandbox = null;
            try
            {
                sandbox = BuildSandbox();
                body();
                _passed++;
                Debug.Log($"[SmokeTests] ✓ {name}");
            }
            catch (Exception e)
            {
                _failures.Add($"{name} — {e.Message}");
            }
            finally
            {
                TeardownSandbox(sandbox);
            }
        }

        // ------------------------------------------------------------------
        // 用例
        // ------------------------------------------------------------------
        static void Test_DatabaseHasThreeDays()
        {
            AreEqual(3, GameDatabase.DayCount, "GameDatabase.DayCount");

            for (int day = 1; day <= 3; day++)
            {
                var plan = GameDatabase.GetDay(day);
                IsTrue(plan != null, $"第 {day} 天的 DayPlan 不存在");
                IsTrue(plan.spawns.Count > 0, $"第 {day} 天没有配置顾客波次");
                IsTrue(plan.businessSeconds > 0f, $"第 {day} 天的营业时长非法");
            }

            // 第三天是唯一有检查员的一天，结局判定依赖它
            IsTrue(GameDatabase.GetDay(3).spawnInspector, "第三天没有开启检查员");
            IsTrue(!GameDatabase.GetDay(1).spawnInspector, "第一天不该有检查员");
        }

        static void Test_EveryShelfHasProduct()
        {
            var shelves = Game.Store.Shelves;
            IsTrue(shelves.Count > 0, "店里一个货架都没有");

            for (int i = 0; i < shelves.Count; i++)
                IsTrue(shelves[i].product != null,
                       $"第 {i} 个货架没有绑定商品（AddShelf 的 productId 写错了）");

            IsTrue(Game.Store.SalesShelfCount() > 0, "没有任何对外销售的货架");
            IsTrue(Game.Store.SalesShelfCount() < shelves.Count,
                   "清洁用品架应该被排除在销售货架之外");
        }

        /// <summary>
        /// 回归用例。
        ///
        /// GameManager.CloseStore() 的顺序是
        ///   StopDay → ForceEveryoneOut → Events.EndDay() → CloseCheckout → EnterSettlement()，
        /// 而 EnterSettlement() 当帧就调 DayManager.BuildSummary()。
        ///
        /// 检查员是走到门口才触发 InspectorBehaviour.OnLeaveStore 的，要好几秒。
        /// 修复前：摘要里是「检查员评价：未完成 ✗」、goalsMet=false、声望快照少算检查加减分，
        /// 几秒后评级才落到 Game.Day 上，而结局判定读的是那个迟到的新值 —— 两边对不上。
        /// 修复后：EndDay() 会在结算之前把还没出结果的检查先结算掉。
        /// </summary>
        static void Test_PendingInspectionResolvedBeforeSettlement()
        {
            EnterDay(3);
            SpawnInspectorInStore();

            IsTrue(!Game.Day.InspectionDone, "前置条件不成立：检查不该提前完成");

            // 模拟 CloseStore()：顾客被赶出去，但还在往门口走（没触发 OnLeaveStore）
            Game.Spawner.ForceEveryoneOut();
            Game.Events.EndDay();

            IsTrue(Game.Day.InspectionDone,
                   "EndDay() 之后检查仍未结算 —— 结算摘要会显示「未完成」");

            var summary = Game.Day.BuildSummary();
            IsTrue(summary.goalReport.Contains("检查员评价"),
                   "结算摘要里没有检查员这一行");
            IsTrue(!summary.goalReport.Contains("未完成"),
                   $"结算摘要仍然显示检查未完成：\n{summary.goalReport}");
        }

        static void Test_InspectionIsNotAppliedTwice()
        {
            EnterDay(3);
            SpawnInspectorInStore();

            Game.Events.EndDay();

            var gradeAfterFirst = Game.Day.InspectionResult;
            int repAfterFirst = Game.Reputation.Value;

            // 声望起点定在 50，评级加减分不会撞上 0/100 的钳制，
            // 否则「重复结算」会被钳制掩盖成看起来没变。
            IsTrue(repAfterFirst > 0 && repAfterFirst < 100,
                   $"声望 {repAfterFirst} 贴边了，这个用例失去意义");

            Game.Events.EndDay();

            AreEqual(gradeAfterFirst.ToString(), Game.Day.InspectionResult.ToString(),
                     "重复 EndDay 之后的检查评级");
            AreEqual(repAfterFirst, Game.Reputation.Value,
                     "重复 EndDay 之后的声望（检查加减分被重复应用了）");
        }

        static void Test_NoInspectionOnDaysWithoutInspector()
        {
            EnterDay(1);
            SpawnInspectorInStore();   // 就算店里真站着一个检查员

            Game.Events.EndDay();

            IsTrue(!Game.Day.InspectionDone,
                   "第一天不该产生检查结果 —— DayPlan.spawnInspector 是关的");

            var summary = Game.Day.BuildSummary();
            IsTrue(!summary.goalReport.Contains("检查员评价"),
                   $"第一天的结算摘要里不该有检查员这一行：\n{summary.goalReport}");
        }

        /// <summary>
        /// 回归用例。
        ///
        /// CustomerController.LeaveAngry 原来只挡 Leaving、不挡 Angry，
        /// 但顾客可以在耐心还没归零时就变成 Angry（「店里太脏」「缺货」「收银太慢」
        /// 「你猜错了」「被请出了店」五条路都是）。之后耐心继续掉到 0，
        /// ApplyPatience 会再调一次 LeaveAngry("等太久了")：
        /// 声望扣两次 -6，DayManager.LeftAngry 把一个人记成两个。
        ///
        /// LeftAngry 是第三天检查员「有没有顾客被气走」那一项的输入
        /// （0 个 +2 分，1 个 +1 分，2 个及以上 0 分），声望又同时是
        /// 第二/三天的过关条件和结局判定的输入，所以这个重复结算会一路串到结局。
        /// </summary>
        static void Test_AngryCustomerIsCountedOnlyOnce()
        {
            EnterDay(3);
            var customer = SpawnCustomer(MonsterType.Vampire);

            // 第一次生气离店：耐心还没归零（模拟「店里太脏了」那条路）
            customer.LeaveAngry("测试：店里太脏了");

            IsTrue(customer.Patience > 0f,
                   "前置条件不成立：这个用例要求顾客变成 Angry 时耐心还没归零");
            AreEqual(1, Game.Day.LeftAngry, "第一次生气离店后的 LeftAngry");

            int repAfterFirst = Game.Reputation.Value;
            IsTrue(repAfterFirst > 0 && repAfterFirst < 100,
                   $"声望 {repAfterFirst} 贴边了，重复扣分会被钳制掩盖，用例失去意义");

            // 耐心掉到 0 —— 修复前这里会触发第二次 LeaveAngry("等太久了")
            customer.ApplyPatience(-99999f);

            AreEqual(1, Game.Day.LeftAngry,
                     "同一个顾客被记成了两次生气离店（LeaveAngry 没有把 Angry 当终态）");
            AreEqual(repAfterFirst, Game.Reputation.Value,
                     "同一个顾客被扣了两次声望");
        }

        /// <summary>
        /// 上一条的跨天版本：营业结束时 Angry 顾客还在往门口走，
        /// 玩家点「进入下一天」后 PrepareDay() 把计数清零，
        /// 这时那个顾客的耐心才掉到 0 —— 修复前会把 -6 声望和一次生气离店
        /// 记到全新的一天头上。
        /// </summary>
        static void Test_AngryCustomerDoesNotBleedIntoNextDay()
        {
            // 用吸血鬼而不是狼人：第二天是满月，WerewolfBehaviour.OnEnterStore
            // 会去开满月警告对话框，而这个沙箱里没有装 UIRoot。
            EnterDay(2);
            var customer = SpawnCustomer(MonsterType.Vampire);

            customer.LeaveAngry("测试：收银太慢");
            AreEqual(1, Game.Day.LeftAngry, "第二天的 LeftAngry");

            // 进入第三天：计数清零，顾客却还留在场上
            EnterDay(3);
            AreEqual(0, Game.Day.LeftAngry, "新一天的 LeftAngry 应该是 0");

            int repAtDayStart = Game.Reputation.Value;
            customer.ApplyPatience(-99999f);

            AreEqual(0, Game.Day.LeftAngry,
                     "上一天的顾客把生气离店记到了新一天头上");
            AreEqual(repAtDayStart, Game.Reputation.Value,
                     "上一天的顾客在新一天又扣了一次声望");
        }

        /// <summary>
        /// 回归用例。
        ///
        /// GameManager.EvaluateEnding 用的是三天累计利润 _totalProfit
        /// （和 GameConfig.EndingExcellentProfit = 150 比），但它原来既没写进
        /// SaveData，StartNewRun 又无条件把它清零。于是玩家中途退出再进来，
        /// 前面几天的利润全部蒸发，「优秀结局」几乎不可能拿到。
        ///
        /// 这里按 GameBootstrap.BootGame() 的真实顺序验证：先 Apply，再把
        /// 存档里的累计利润交给 StartNewRun —— 反过来写会被 StartNewRun 冲掉。
        /// </summary>
        static void Test_TotalProfitSurvivesSaveLoad()
        {
            WithIsolatedSaveFile(() =>
            {
                // 模拟：前两天打完累计 137 利润，现在停在第三天
                Game.Manager.ResetRunState(3, 137);
                Game.Economy.SetMoney(212);
                Game.Reputation.SetValue(64);
                AreEqual(137, Game.Manager.TotalProfit, "存档前的累计利润");

                SaveSystem.Save();

                var save = SaveSystem.Load();
                IsTrue(save != null, "存档没读回来");
                AreEqual(137, save.totalProfit, "存档文件里的累计利润");
                AreEqual(3, save.currentDay, "存档文件里的天数");

                // 模拟重启：新进程里 GameManager 是干净的
                Game.Manager.ResetRunState(1, 0);
                AreEqual(0, Game.Manager.TotalProfit, "前置条件：重开后累计利润应为 0");

                // GameBootstrap.BootGame() 的读档顺序
                SaveSystem.Apply(save);
                Game.Manager.ResetRunState(save.currentDay, save.totalProfit);

                AreEqual(137, Game.Manager.TotalProfit, "读档后没有恢复累计利润");
                AreEqual(3, Game.Day.CurrentDay, "读档后的天数");
                AreEqual(212, Game.Economy.Money, "读档后的金钱");
                AreEqual(64, Game.Reputation.Value, "读档后的声望");
            });
        }

        /// <summary>
        /// totalProfit 是纯新增字段，没有 bump SaveVersion —— 所以修复前那一版
        /// 写出来的存档（JSON 里根本没有这个键）必须照样能读，
        /// 并且累计利润取默认值 0，也就是和修复前一模一样的行为。
        /// </summary>
        static void Test_LegacySaveWithoutTotalProfitStillLoads()
        {
            WithIsolatedSaveFile(() =>
            {
                // 修复前的存档就长这样
                string legacy =
                    "{\n" +
                    $"    \"version\": {GameConfig.SaveVersion},\n" +
                    "    \"currentDay\": 2,\n" +
                    "    \"money\": 118,\n" +
                    "    \"reputation\": 47,\n" +
                    "    \"unlockedProducts\": [],\n" +
                    "    \"discoveredMonsters\": [],\n" +
                    "    \"checkoutLevel\": 1,\n" +
                    "    \"sfxVolume\": 0.55,\n" +
                    "    \"musicVolume\": 0.22\n" +
                    "}";
                File.WriteAllText(SaveSystem.FilePath, legacy);

                var save = SaveSystem.Load();

                IsTrue(save != null,
                       "旧存档被拒收了 —— 只是加一个字段，不该把玩家进行中的进度作废");
                AreEqual(2, save.currentDay, "旧存档的天数");
                AreEqual(118, save.money, "旧存档的金钱");
                AreEqual(47, save.reputation, "旧存档的声望");
                AreEqual(1, save.checkoutLevel, "旧存档的收银台等级");
                AreEqual(0, save.totalProfit, "旧存档缺这个键，累计利润该默认为 0");

                // 默认值要能安全地走完恢复路径
                SaveSystem.Apply(save);
                Game.Manager.ResetRunState(save.currentDay, save.totalProfit);

                AreEqual(0, Game.Manager.TotalProfit, "旧存档恢复后的累计利润");
                AreEqual(2, Game.Day.CurrentDay, "旧存档恢复后的天数");
                AreEqual(118, Game.Economy.Money, "旧存档恢复后的金钱");
            });
        }

        /// <summary>
        /// 回归用例。
        ///
        /// FinishRun() 进 GameOver 之后照样 SaveSystem.Save()，而 Capture() 记的
        /// currentDay 仍是最后一天。修复前存档里没有任何「这局打完了」的痕迹，
        /// 于是 BootGame() 把它当进度恢复 → StartNewRun(3, …) → 又是第三天准备阶段。
        /// 玩家看不到结局，只能一遍遍重打第三天；结局界面的「再开一局」
        /// （InfoPanels.cs 的 GameBootstrap.RestartGame）走的是同一条路径，
        /// 所以那个按钮同样开不了新局。
        ///
        /// 叠加上一轮的 totalProfit 持久化后还会更糟：每重打一次第三天，
        /// EnterSettlement 就再 += 一次当日利润，累计利润单调膨胀，
        /// EvaluateEnding 的 150 门槛会被虚高的数字蒙混过去。
        /// </summary>
        static void Test_FinishedRunStartsFreshOnReboot()
        {
            WithIsolatedSaveFile(() =>
            {
                // 打完三天：累计 240 利润、身上 300、声望 75，图鉴解锁了狼人
                Game.Manager.ResetRunState(3, 240);
                Game.Economy.SetMoney(300);
                Game.Reputation.SetValue(75);
                Game.Store.Checkout.SetLevel(1);
                Game.Day.InspectionDone = true;
                Game.Day.InspectionResult = InspectionGrade.A;
                BestiaryTracker.Discover(MonsterType.Werewolf);

                Game.Manager.ConcludeRun();
                SaveSystem.Save();

                var save = SaveSystem.Load();
                IsTrue(save != null, "终局存档没读回来");
                IsTrue(save.runCompleted, "终局存档没有标记这一局已经打完");
                AreEqual(3, save.currentDay, "终局存档里的天数仍然是最后一天（这正是问题所在）");

                // 模拟重启：各管理器回到 BuildManagers 刚装配好的初始值
                Game.Economy.SetMoney(GameConfig.StartingMoney);
                Game.Reputation.SetValue(GameConfig.StartingReputation);
                Game.Store.Checkout.SetLevel(0);
                Game.Manager.ResetRunState(1, 0);
                BestiaryTracker.Reset();

                // GameBootstrap.BootGame() 的读档决策。
                // false = 结局界面的「再开一局」走的就是这条（它不传 freshRun，
                // 靠存档自己的 runCompleted 开新局）。
                bool resume = SaveSystem.ShouldResume(save, false);
                IsTrue(!resume, "通关存档被当成进度恢复了 —— 玩家会被丢回最后一天");

                SaveSystem.Apply(save, resume);
                Game.Manager.ResetRunState(resume ? save.currentDay : 1,
                                           resume ? save.totalProfit : 0);

                AreEqual(1, Game.Day.CurrentDay, "通关后重进应该从第一天开始");
                AreEqual(0, Game.Manager.TotalProfit, "通关后重进应该清空累计利润");
                AreEqual(GameConfig.StartingMoney, Game.Economy.Money, "通关后重进应该回到初始资金");
                AreEqual(GameConfig.StartingReputation, Game.Reputation.Value, "通关后重进应该回到初始声望");
                AreEqual(0, Game.Store.Checkout.Level, "通关后重进不该继承上一局的收银台升级");

                // 图鉴是跨局累积的，这部分要留着
                IsTrue(BestiaryTracker.IsDiscovered(MonsterType.Werewolf),
                       "通关后重进把图鉴也清掉了 —— 那是跨局累积的");
            });
        }

        /// <summary>
        /// 反向保证：没打完的存档必须照常续玩。
        /// 顺带覆盖向后兼容 —— 旧存档 JSON 里根本没有 runCompleted 这个键，
        /// 读出来是 false，也就是「未通关」，和修复前的行为一致。
        /// </summary>
        static void Test_UnfinishedRunStillResumes()
        {
            WithIsolatedSaveFile(() =>
            {
                // 第二天准备阶段存的档（State 不是 GameOver）
                Game.Manager.ResetRunState(2, 96);
                Game.Economy.SetMoney(151);
                Game.Reputation.SetValue(53);
                SaveSystem.Save();

                var save = SaveSystem.Load();
                IsTrue(save != null, "进行中的存档没读回来");
                IsTrue(!save.runCompleted, "没打完的存档不该被标记成已通关");
                IsTrue(SaveSystem.ShouldResume(save, false), "没打完的存档应该续玩");

                Game.Manager.ResetRunState(1, 0);
                SaveSystem.Apply(save, true);
                Game.Manager.ResetRunState(save.currentDay, save.totalProfit);

                AreEqual(2, Game.Day.CurrentDay, "续玩后的天数");
                AreEqual(96, Game.Manager.TotalProfit, "续玩后的累计利润");
                AreEqual(151, Game.Economy.Money, "续玩后的金钱");

                // 旧存档：JSON 里没有 runCompleted 这个键
                string legacy =
                    "{\n" +
                    $"    \"version\": {GameConfig.SaveVersion},\n" +
                    "    \"currentDay\": 2,\n" +
                    "    \"money\": 118,\n" +
                    "    \"reputation\": 47,\n" +
                    "    \"unlockedProducts\": [],\n" +
                    "    \"discoveredMonsters\": [],\n" +
                    "    \"checkoutLevel\": 0,\n" +
                    "    \"sfxVolume\": 0.55,\n" +
                    "    \"musicVolume\": 0.22\n" +
                    "}";
                File.WriteAllText(SaveSystem.FilePath, legacy);

                var legacySave = SaveSystem.Load();
                IsTrue(legacySave != null, "旧存档被拒收了");
                IsTrue(!legacySave.runCompleted, "旧存档缺这个键，该默认为 false");
                IsTrue(SaveSystem.ShouldResume(legacySave, false),
                       "旧存档应该照常续玩 —— 加字段不能改变它的行为");
            });
        }

        /// <summary>
        /// 回归用例。
        ///
        /// EnterSettlement 是在「当日利润已经计入 _totalProfit」之后、
        /// 「ContinueAfterSettlement 推进天数」之前存的档，而 Capture() 记的
        /// currentDay 还是当天。玩家在结算界面退出再进来，会被恢复成当天的
        /// 准备阶段，重打一遍当天，结算时把当日利润再加进累计一次。
        /// </summary>
        static void Test_SettledDayResumesAtNextDay()
        {
            WithIsolatedSaveFile(() =>
            {
                // 第二天，前一天累计 40
                Game.Manager.ResetRunState(2, 40);
                Game.Day.PrepareDay();

                // 当日：卖出 60、成本 20 → DayProfit = 40
                Game.Economy.RecordSale(60);
                Game.Economy.RecordCostOfGoodsSold(20);

                var summary = Game.Manager.ConcludeDay();
                AreEqual(40, summary.profit, "当日利润");
                AreEqual(80, Game.Manager.TotalProfit, "结算后的累计利润");

                var save = SaveSystem.Load();
                IsTrue(save != null, "结算存档没读回来");
                IsTrue(save.daySettled, "结算存档没有标记当天已结算");
                AreEqual(2, save.currentDay, "结算存档里的天数仍是当天（这正是问题所在）");
                AreEqual(80, save.totalProfit, "结算存档里的累计利润");
                AreEqual(3, SaveSystem.ResumeDay(save),
                         "结算存档应该从下一天接着打，而不是重打当天");

                // 模拟重启
                Game.Manager.ResetRunState(1, 0);
                Game.Economy.SetMoney(GameConfig.StartingMoney);
                Game.Economy.ResetDaily();

                bool resume = SaveSystem.ShouldResume(save, false);
                IsTrue(resume, "没打完的存档应该续玩");

                SaveSystem.Apply(save, resume);
                Game.Manager.ResetRunState(SaveSystem.ResumeDay(save), save.totalProfit);

                AreEqual(3, Game.Day.CurrentDay, "结算后退出重进重打了当天");
                AreEqual(80, Game.Manager.TotalProfit, "重进后的累计利润被改动了");

                // 接着把这一天打完：累计里不能出现第二遍的第二天利润
                Game.Day.PrepareDay();
                Game.Economy.RecordSale(30);
                Game.Economy.RecordCostOfGoodsSold(10);
                Game.Manager.ConcludeDay();

                AreEqual(100, Game.Manager.TotalProfit,
                         "第二天的利润被重复累计了（80 + 20 才对）");
            });
        }

        /// <summary>
        /// 用户明确要求「取消三天限制，变成无限连续经营」——第三天结算后不再
        /// 强制结局，日子按已有的三套 DayPlan 循环下去（第 4 天用第 1 天的内容，
        /// 以此类推），检查员照旧每逢第 3 天来一次，只是不再触发 GameOver。
        /// </summary>
        static void Test_DayLoopContinuesPastDayThreeWithNoForcedEnding() => WithIsolatedSaveFile(() =>
        {
            Game.Manager.ResetRunState(1, 0);
            Game.Manager.BeginNewDay();

            for (int day = 1; day <= 5; day++)
            {
                IsTrue(Game.Day.CurrentPlan != null, $"第 {day} 天没有对应的 DayPlan —— 循环断了");

                bool expectInspector = day % 3 == 0;
                if (expectInspector)
                    IsTrue(Game.Day.CurrentPlan.spawnInspector, $"第 {day} 天该有检查员却没有");
                else
                    IsTrue(!Game.Day.CurrentPlan.spawnInspector, $"第 {day} 天不该有检查员却来了");

                Game.Manager.OpenStore();
                Game.Manager.ConcludeDay();
                Game.Manager.ContinueAfterSettlement();

                AreEqual((int)GameState.MorningBrief, (int)Game.Manager.State,
                    $"第 {day} 天结算后应该直接进下一天的晨会，而不是弹结局（无限连续经营）");
            }

            AreEqual(6, Game.Day.CurrentDay, "跑完 5 天结算后应该停在第 6 天");
        });

        /// <summary>
        /// 回归用例。
        ///
        /// 暂停菜单的按钮写着「重新开始」，但它走的是
        /// GameManager.RestartRun → GameBootstrap.RestartGame → BootGame，
        /// 而 BootGame 会把存档重新读回来。本局那份存档还没打完
        /// （runCompleted = false），于是被当成进度恢复 —— 玩家点了「重新开始」
        /// 却回到了原来的进度，文案和行为对不上。
        ///
        /// 边界：只丢本局进度（天数 / 金钱 / 声望 / 收银台 / 累计利润），
        /// 图鉴和音量是跨局累积的，走 Apply 的跨局分支保留。
        /// </summary>
        static void Test_ManualRestartDiscardsRunButKeepsCrossRunData()
        {
            WithIsolatedSaveFile(() =>
            {
                // 第二天进行中：累计 96、身上 151、声望 53、收银台已升级、图鉴解锁了幽灵
                Game.Manager.ResetRunState(2, 96);
                Game.Economy.SetMoney(151);
                Game.Reputation.SetValue(53);
                Game.Store.Checkout.SetLevel(1);
                BestiaryTracker.Discover(MonsterType.Ghost);
                SaveSystem.Save();

                var save = SaveSystem.Load();
                IsTrue(save != null, "存档没读回来");
                IsTrue(!save.runCompleted, "前置条件：这一局还没打完");

                // 没点重新开始（普通读档、以及结局界面的「再开一局」）→ 决策不变
                IsTrue(SaveSystem.ShouldResume(save, false),
                       "普通读档续玩被破坏了");

                // 点了暂停菜单的「重新开始」→ 不许续玩
                IsTrue(!SaveSystem.ShouldResume(save, true),
                       "主动「重新开始」仍然续了上一局的进度");

                // 模拟重启后 BuildManagers 刚装配好的初值
                Game.Economy.SetMoney(GameConfig.StartingMoney);
                Game.Reputation.SetValue(GameConfig.StartingReputation);
                Game.Store.Checkout.SetLevel(0);
                Game.Manager.ResetRunState(1, 0);
                BestiaryTracker.Reset();

                // GameBootstrap.BootGame() 在 freshRun = true 下的读档路径
                bool resume = SaveSystem.ShouldResume(save, true);
                SaveSystem.Apply(save, resume);
                Game.Manager.ResetRunState(resume ? SaveSystem.ResumeDay(save) : 1,
                                           resume ? save.totalProfit : 0);

                AreEqual(1, Game.Day.CurrentDay, "重新开始应该回到第一天");
                AreEqual(0, Game.Manager.TotalProfit, "重新开始应该清空累计利润");
                AreEqual(GameConfig.StartingMoney, Game.Economy.Money, "重新开始应该回到初始资金");
                AreEqual(GameConfig.StartingReputation, Game.Reputation.Value, "重新开始应该回到初始声望");
                AreEqual(0, Game.Store.Checkout.Level, "重新开始不该继承上一局的收银台升级");

                // 图鉴和音量在 Apply 里是同一个跨局分支，图鉴还在就说明那一段走到了
                IsTrue(BestiaryTracker.IsDiscovered(MonsterType.Ghost),
                       "重新开始把图鉴也清掉了 —— 那是跨局累积的");
            });
        }

        /// <summary>
        /// 回归用例。
        ///
        /// CheckoutView 的 Esc 关不掉（CanCloseWithEscape = false），所以收银中按 Esc
        /// 会走到 GameManager.Pause()：暂停菜单叠上来，收银界面仍然开着。
        /// 修复前 CheckoutView.Update 只看 IsOpen、不看游戏状态，于是暂停期间照样
        /// 累计收银耗时、扣当前顾客和整条队伍的耐心，掉到 0 还会愤怒离店 ——
        /// 玩家人在暂停菜单里，声望 -6 和 LeftAngry +1 就已经发生了。
        /// </summary>
        static void Test_CheckoutSessionFrozenWhilePaused()
        {
            EnterDay(2);
            Game.Manager.OpenStore();

            // 队首 + 后面一位，队列里的人也会掉耐心
            var head = SpawnCustomer(MonsterType.Vampire);
            var behind = SpawnCustomer(MonsterType.Slime);

            var checkout = Game.Store.Checkout;
            checkout.Enqueue(head);
            checkout.Enqueue(behind);

            // 把队首耐心压到接近 0：只要推进一帧就会愤怒离店
            head.ApplyPatience(-(head.Patience - 0.4f));
            IsTrue(head.Patience > 0f && head.Patience < 1f,
                   "前置条件：队首耐心应该压到接近 0");

            // ---- 对照：营业中必须真的推进，否则这个用例是空的 ----
            float t = CheckoutView.AdvanceSession(checkout, head, 0f, 0.016f);
            IsTrue(t > 0f, "营业中收银会话没有推进 —— 用例失去意义");
            IsTrue(head.Patience < 0.4f, "营业中队首没有掉耐心 —— 用例失去意义");
            IsTrue(behind.Patience < behind.MaxPatience, "营业中队列里的人没有掉耐心");

            // ---- 进入暂停 ----
            IsTrue(Game.Manager.EnterPauseState(), "没能进入暂停状态");

            float sessionTime = t;
            float headPatience = head.Patience;
            float behindPatience = behind.Patience;
            int rep = Game.Reputation.Value;
            int angry = Game.Day.LeftAngry;

            IsTrue(headPatience > 0f, "前置条件：暂停时队首还没离店");

            // 暂停期间推进若干帧，且步长足够大 —— 没有闸门的话队首必然被扣到 0
            for (int i = 0; i < 30; i++)
                sessionTime = CheckoutView.AdvanceSession(checkout, head, sessionTime, 0.05f);

            AreEqualFloat(t, sessionTime, "暂停期间收银耗时被累计了");
            AreEqualFloat(headPatience, head.Patience, "暂停期间队首顾客掉了耐心");
            AreEqualFloat(behindPatience, behind.Patience, "暂停期间队列里的顾客掉了耐心");
            AreEqual(rep, Game.Reputation.Value, "暂停期间声望被改动了");
            AreEqual(angry, Game.Day.LeftAngry, "暂停期间产生了愤怒离店");
            AreEqual(2, checkout.QueueLength, "暂停期间有人被挤出了队伍");
        }

        /// <summary>
        /// 用户反馈明确要求「午夜营业支持开启倍速模式，支持 1/1.25/1.5/2/2.5/3 倍速」。
        /// 走的是全局 Time.timeScale：只在营业中才该动它，且不能带出营业阶段。
        /// </summary>
        static void Test_BusinessSpeedAppliesDuringOpenOnly()
        {
            try
            {
                AreEqualFloat(1f, Game.Manager.BusinessSpeed, "前置条件：默认应该是 1 倍速");

                // 不在营业中调倍速：记下来，但不该立即改全局时间流速
                Game.Manager.SetBusinessSpeed(2f);
                AreEqualFloat(2f, Game.Manager.BusinessSpeed, "倍速没有被记住");
                AreEqualFloat(1f, Time.timeScale, "不在营业中就不该动全局时间流速");

                // 开门营业：应用记下来的倍速
                Game.Manager.ResetRunState(1, 0);
                Game.Manager.BeginNewDay();
                Game.Manager.OpenStore();
                AreEqualFloat(2f, Time.timeScale, "开门营业没有应用记下来的倍速");

                // 营业中再切，立即生效
                Game.Manager.SetBusinessSpeed(3f);
                AreEqualFloat(3f, Time.timeScale, "营业中切倍速没有立即生效");

                // 结算：倍速不能带出营业阶段
                Game.Manager.ConcludeDay();
                AreEqualFloat(1f, Time.timeScale, "结算之后倍速还留在营业阶段的设定上");
                AreEqualFloat(3f, Game.Manager.BusinessSpeed, "结算之后玩家选的倍速档位不该被清空");
            }
            finally
            {
                Time.timeScale = 1f;   // 防止这条用例的倍速泄漏给后面的用例
            }
        }

        /// <summary>营业倍速是跨局偏好，和音量一样存能读、旧存档没有这个字段也不能炸。</summary>
        static void Test_BusinessSpeedSurvivesSaveLoad() => WithIsolatedSaveFile(() =>
        {
            try
            {
                Game.Manager.ResetRunState(1, 0);
                Game.Manager.SetBusinessSpeed(2.5f);
                SaveSystem.Save();

                var save = SaveSystem.Load();
                IsTrue(save != null, "存档没读回来");
                AreEqualFloat(2.5f, save.businessSpeed, "倍速没有存进去");

                Game.Manager.SetBusinessSpeed(1f);
                SaveSystem.Apply(save, true);
                AreEqualFloat(2.5f, Game.Manager.BusinessSpeed, "读档后倍速没有恢复");

                // 旧存档没有这个字段：应该退回 1x，不能是 0（否则营业时会把时间冻结掉）
                var legacy = new SaveData { currentDay = 1 };
                SaveSystem.Apply(legacy, true);
                AreEqualFloat(1f, Game.Manager.BusinessSpeed, "旧存档没有倍速字段时应该退回 1x");
            }
            finally
            {
                Time.timeScale = 1f;
            }
        });

        /// <summary>
        /// 回归用例。
        ///
        /// 设计文档 §10 第二天的目标是「至少服务 5 名顾客，声望达到 40，
        /// 不让狼人破坏超过一个货架」，DayPlan.goalDescription 也照抄给玩家看了。
        /// 但 BuildSummary 只判前两项：ShelvesKnockedOver 由
        /// RandomEventManager 累加、PrepareDay 重置，全仓库没有任何读取处 ——
        /// 写了一半掉了的判定。玩家撞倒 5 个货架照样算「达成目标」。
        /// </summary>
        static void Test_ShelfCrashGoalIsEvaluated()
        {
            EnterDay(2);

            var plan = Game.Day.CurrentPlan;
            IsTrue(plan.goalMaxShelvesKnocked >= 0,
                   "第二天应该有「撞倒货架」这项目标（简报里写了）");

            // 把其余两项目标都满足，只留货架这一个变量
            for (int i = 0; i < plan.goalCustomersServed; i++) Game.Day.RecordServed(null);
            Game.Reputation.SetValue(Mathf.Max(plan.goalMinReputation, 50));

            // 正好卡在上限：算达成
            Game.Day.ShelvesKnockedOver = plan.goalMaxShelvesKnocked;
            var ok = Game.Day.BuildSummary();
            IsTrue(ok.goalReport.Contains("货架"), $"结算摘要里没有货架这一行：\n{ok.goalReport}");
            IsTrue(ok.goalsMet, $"没超上限却判成未达成：\n{ok.goalReport}");

            // 超一个：必须判为未达成
            Game.Day.ShelvesKnockedOver = plan.goalMaxShelvesKnocked + 1;
            var bad = Game.Day.BuildSummary();
            IsTrue(!bad.goalsMet,
                   $"撞倒的货架超了上限，目标却仍判为达成：\n{bad.goalReport}");

            // 没有这项目标的日子不该凭空多一行
            EnterDay(1);
            var day1 = Game.Day.BuildSummary();
            IsTrue(!day1.goalReport.Contains("货架"),
                   $"第一天没有这项目标，不该出现货架行：\n{day1.goalReport}");
        }

        /// <summary>
        /// 回归用例。
        ///
        /// 设计文档 §15 要求存档保存「仓库商品」，但 SaveData 里既没有仓库
        /// 也没有货架库存。玩家在准备阶段花钱进的货，重进后凭空消失，
        /// 钱却已经扣掉了（结算存档记的是花完钱之后的余额）—— 净亏一笔。
        /// </summary>
        static void Test_StockSurvivesSaveLoad()
        {
            WithIsolatedSaveFile(() =>
            {
                EnterDay(2);

                var soda = GameDatabase.GetProduct("blood_orange_soda");
                var jelly = GameDatabase.GetProduct("glow_jelly");
                IsTrue(soda != null && jelly != null, "商品表里找不到测试用的商品");

                // 进货 7 瓶汽水放仓库，果冻上架 4 件
                Game.Store.AddToWarehouse(soda, 7);
                var jellyShelf = Game.Store.FindShelf(jelly);
                IsTrue(jellyShelf != null, "找不到发光果冻的货架");
                jellyShelf.AddStock(4);

                SaveSystem.Save();

                var save = SaveSystem.Load();
                IsTrue(save != null, "存档没读回来");
                AreEqual(7, FindStock(save.warehouse, "blood_orange_soda"), "存档里的仓库汽水数");
                AreEqual(4, FindStock(save.shelfStock, "glow_jelly"), "存档里的货架果冻数");

                // 模拟重启：StoreWorld.Build() 之后仓库和货架都是空的
                for (int i = 0; i < GameDatabase.Products.Count; i++)
                    Game.Store.Warehouse[GameDatabase.Products[i]] = 0;
                for (int i = 0; i < Game.Store.Shelves.Count; i++)
                {
                    Game.Store.Shelves[i].count = 0;
                    Game.Store.Shelves[i].Refresh();
                }
                AreEqual(0, Game.Store.WarehouseCount(soda), "前置条件：重启后仓库应该是空的");

                SaveSystem.Apply(save, true);

                AreEqual(7, Game.Store.WarehouseCount(soda), "重进后仓库里的汽水没了");
                AreEqual(4, Game.Store.FindShelf(jelly).count, "重进后货架上的果冻没了");

                // 旧存档没有这两个字段 → 空列表 → 仓库和货架都是空的，和修复前一致
                var legacy = new SaveData { currentDay = 2 };
                legacy.warehouse = null;
                legacy.shelfStock = null;
                SaveSystem.Apply(legacy, true);
                AreEqual(0, Game.Store.WarehouseCount(soda), "旧存档应该恢复成空仓库，而不是抛异常");
            });
        }

        static int FindStock(List<StockEntry> list, string productId)
        {
            if (list == null) return 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].productId == productId) return list[i].count;
            return 0;
        }

        // ------------------------------------------------------------------
        // 白天异世界进货 — 设计文档 §3 / §18 第一阶段
        // ------------------------------------------------------------------

        /// <summary>
        /// §4.1 核心规则：「每只怪物必须同时拥有远征功能、店内功能、性格副作用三项设计」。
        /// 缺任何一项，第四阶段的双岗位取舍就立不起来。
        /// </summary>
        static void Test_ExpeditionDataIsComplete()
        {
            var staff = GameDatabase.Staff;
            AreEqual(4, staff.Count, "员工数量（§1.5 原型规模：4 名怪物员工）");

            for (int i = 0; i < staff.Count; i++)
            {
                var s = staff[i];
                IsTrue(!string.IsNullOrEmpty(s.staffId), $"第 {i} 名员工没有 id");
                IsTrue(!string.IsNullOrEmpty(s.expeditionPassive), $"{s.displayName} 缺远征功能");
                IsTrue(!string.IsNullOrEmpty(s.storeAbility), $"{s.displayName} 缺店内功能");
                IsTrue(!string.IsNullOrEmpty(s.sideEffect), $"{s.displayName} 缺性格副作用");
                IsTrue(!string.IsNullOrEmpty(s.skillName), $"{s.displayName} 缺主动技能");
                IsTrue(s.maxHealth > 0f && s.moveSpeed > 0f, $"{s.displayName} 的数值非法");
                IsTrue(s.skillCooldown > 0f, $"{s.displayName} 的技能冷却必须大于 0");
            }

            // 默认队伍里的 id 必须都能查到
            for (int i = 0; i < GameDatabase.DefaultSquad.Length; i++)
                IsTrue(GameDatabase.GetStaff(GameDatabase.DefaultSquad[i]) != null,
                       $"默认队伍里的 {GameDatabase.DefaultSquad[i]} 在员工表里不存在");

            var enemy = GameDatabase.GetEnemy(GameDatabase.DefaultEnemyId);
            IsTrue(enemy != null, "找不到默认敌人");
            IsTrue(enemy.telegraphSeconds > 0f,
                   "§3.3 要求敌人攻击必须有清晰前摇，前摇时长不能是 0");
            IsTrue(GameDatabase.GetProduct(enemy.lootProductId) != null,
                   $"敌人掉落的 {enemy.lootProductId} 不在商品表里");
        }

        static void Test_ExpeditionRoomIsWalkable() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                var world = Game.Expedition.World;
                IsTrue(world != null, "房间没建出来");
                IsTrue(world.Grid.IsWalkable(world.CampCell), "入口营地落在了墙里");

                for (int i = 0; i < 12; i++)
                {
                    var cell = world.RandomWalkableCell();
                    IsTrue(world.Grid.IsWalkable(cell), $"刷怪点 {cell} 不可行走");
                }

                // 队长和员工都应该站在可行走格上
                IsTrue(world.Grid.IsWalkable(Game.Expedition.Captain.Cell), "队长出生在墙里");
                var squad = Game.Expedition.Squad;
                IsTrue(squad.Count >= 1, "§18 第一阶段至少要带一名员工出征");
                for (int i = 0; i < squad.Count; i++)
                    IsTrue(world.Grid.IsWalkable(squad[i].Cell), $"{squad[i].Data.displayName} 出生在墙里");
            }
            finally
            {
                Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// §18 第一阶段的完成标准：「玩家能击败敌人并拾取一件可售商品」。
        /// 这里把整条链路走完：出发 → 打死敌人 → 掉落 → 收进背包 → 结束入库，
        /// 也就是 §22 最小可行版本要验证的那条「打怪掉货 → 带回店里」。
        /// </summary>
        static void Test_ExpeditionLootReachesWarehouse() => WithIsolatedSaveFile(() =>
        {
            var soda = GameDatabase.GetProduct("blood_orange_soda");
            IsTrue(soda != null, "商品表里没有血橙汽水");

            int before = Game.Store.WarehouseCount(soda);

            Game.Expedition.Begin();

            AreEqual((int)GameState.Expedition, (int)Game.Manager.State, "出发后的游戏状态");
            IsTrue(Game.Expedition.IsRunning, "远征没有进入进行中状态");
            AdvanceToRoom(RoomKind.Battle);
            IsTrue(Game.Expedition.EnemiesRemaining > 0, "战斗房里应该有敌人");

            // 全部打死 —— 走的是真实的 Health → HandleDeath → OnEnemyDefeated 链路
            KillAllEnemies();
            AreEqual(0, Game.Expedition.EnemiesRemaining, "敌人全死后剩余数应该是 0");
            IsTrue(Game.Expedition.LootOnGround > 0, "击败敌人没有掉落商品");

            // 拾取（正常由队长走近触发，这里直接结算背包）
            Game.Expedition.AddToBag(soda, 2);
            AreEqual(2, Game.Expedition.BagCount, "背包里的战利品数");

            Game.Expedition.Finish(ExpeditionOutcome.Cleared);

            IsTrue(!Game.Expedition.IsRunning, "远征结束后仍是进行中");
            AreEqual((int)GameState.Preparation, (int)Game.Manager.State, "远征结束后应该回到备货阶段");
            AreEqual(before + 2, Game.Store.WarehouseCount(soda),
                     "战利品没有进仓库 —— 这条链路断了就等于白天和夜晚没打通");
        });

        /// <summary>§3.7：失败「损失部分易碎商品」，主动撤退「保留更多商品」。</summary>
        static void Test_FailedExpeditionLosesHalf() => WithIsolatedSaveFile(() =>
        {
            var soda = GameDatabase.GetProduct("blood_orange_soda");
            int before = Game.Store.WarehouseCount(soda);

            Game.Expedition.Begin();
            Game.Expedition.AddToBag(soda, 8);
            Game.Expedition.Finish(ExpeditionOutcome.Failed);

            AreEqual(before + 4, Game.Store.WarehouseCount(soda),
                     "被击退应该只保留一半战利品");

            before = Game.Store.WarehouseCount(soda);

            Game.Expedition.Begin();
            Game.Expedition.AddToBag(soda, 8);
            Game.Expedition.Finish(ExpeditionOutcome.Retreated);

            AreEqual(before + 8, Game.Store.WarehouseCount(soda),
                     "主动撤退应该保留全部战利品");
        });

        /// <summary>
        /// §3.3「上阵 3 名怪物员工……其他成员自动保持队形」。
        /// 队形位必须互不重叠，而且都落在可行走格上 ——
        /// 落在墙里队友就会一直贴着墙推，正是「长期卡住」的主要来源。
        /// </summary>
        static void Test_SquadOfThreeHoldsFormation() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                var squad = Game.Expedition.Squad;
                AreEqual(ExpeditionManager.SquadSize, squad.Count, "上阵人数");

                var captain = Game.Expedition.Captain;
                var grid = Game.Expedition.World.Grid;

                var slots = new List<Vector2>();
                for (int i = 0; i < squad.Count; i++)
                {
                    AreEqual(i, squad[i].SquadIndex, $"第 {i} 名成员的队内序号");

                    var slot = squad[i].FormationSlot(captain, squad.Count);
                    IsTrue(grid.IsWalkable(StoreGrid.WorldToCell(slot)),
                           $"{squad[i].Data.displayName} 的队形位落在墙里");
                    slots.Add(slot);
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    for (int j = i + 1; j < slots.Count; j++)
                    {
                        float d = (slots[i] - slots[j]).magnitude;
                        IsTrue(d > 0.5f,
                               $"第 {i} 和第 {j} 名成员的队形位挤在一起（相距 {d:0.00}）");
                    }
                }

                // 技能热键 1~3 要能一一对上
                for (int i = 0; i < squad.Count; i++)
                    IsTrue(squad[i].SkillReady, $"{squad[i].Data.displayName} 出发时技能应该是就绪的");
            }
            finally
            {
                Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// 用户反馈明确要求「看得到攻击动作、动作完成了才扣血」——
        /// 冷却好瞬间就结算伤害会让战斗看起来像后台数值运算，不像真的在打。
        /// </summary>
        static void Test_StaffAttackDealsDamageOnlyAfterWindupCompletes() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Resource);

                var follower = Game.Expedition.Squad[0];
                var enemy = FirstAliveEnemy();
                IsTrue(enemy != null, "资源房应该有一只普通敌人");

                follower.TeleportTo(enemy.Cell);   // 确保两者贴在一起，不会被「跑远了」判定打空
                float hpBefore = enemy.Health.Current;

                follower.BeginAttack(enemy);
                IsTrue(follower.IsAttacking, "BeginAttack 之后应该处于攻击动作中");
                AreEqualFloat(hpBefore, enemy.Health.Current, "出手动作还没开始就已经扣血了");

                follower.TickAttack(0.01f);
                AreEqualFloat(hpBefore, enemy.Health.Current, "出手动作还没走完就扣血了");

                // 补满出手时长，命中判定应该恰好在这一刻结算
                follower.TickAttack(1f);
                IsTrue(enemy.Health.Current < hpBefore, "出手动作走完了却没有扣血");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>出手过程中目标被别的手段秒了（比如队友技能），这一下该打空，不能报错也不能补刀。</summary>
        static void Test_StaffAttackWhiffsIfTargetDiesDuringWindup() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Resource);

                var follower = Game.Expedition.Squad[0];
                var enemy = FirstAliveEnemy();
                IsTrue(enemy != null, "资源房应该有一只普通敌人");

                follower.TeleportTo(enemy.Cell);
                follower.BeginAttack(enemy);

                enemy.Health.Damage(enemy.Data.maxHealth + 1f);
                IsTrue(!enemy.IsAlive, "前置条件：目标应该已经死了");

                // 命中判定这一刻不该抛异常，也不该对着尸体补一刀 —— 打空之后正常进入收招
                follower.TickAttack(1f);
                IsTrue(follower.IsAttacking, "打空之后应该进入收招阶段，而不是凭空消失");

                // 收招也要能正常走完，不能卡在攻击状态里出不来
                follower.TickAttack(10f);
                IsTrue(!follower.IsAttacking, "收招走完后应该回到待命状态");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// MP 是把技能冷却包装成的展示数值（用户反馈要看到 HP/MP）——
        /// 冷却好=MP 满，刚放完技能=MP 空，技能本身还是纯冷却驱动。
        /// </summary>
        static void Test_ManaPercentReflectsSkillCooldown() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                var follower = Game.Expedition.Squad[0];
                AreEqualFloat(100f, follower.ManaPercent, "还没放过技能，MP 应该是满的");

                IsTrue(follower.TryUseSkill(), "技能应该能放出去");
                AreEqualFloat(0f, follower.ManaPercent, "刚放完技能，MP 应该是空的");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>§3.2「目标标记：优先攻击指定敌人」。</summary>
        static void Test_TargetMarkingCyclesAndClears() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Battle);
                IsTrue(Game.Expedition.EnemiesRemaining >= 2,
                       "这个用例需要至少两个敌人才能验证轮换");
                IsTrue(Game.Expedition.MarkedTarget == null, "出发时不该有标记目标");

                var first = Game.Expedition.MarkNextTarget();
                IsTrue(first != null, "第一次标记没选中任何敌人");
                IsTrue(Game.Expedition.MarkedTarget == first, "标记没有记录下来");

                var second = Game.Expedition.MarkNextTarget();
                IsTrue(second != null && second != first, "再按一次应该换到另一个敌人");

                // 目标死掉后必须自动清除，否则小队会一直盯着尸体
                var marked = Game.Expedition.MarkedTarget;
                marked.Health.Damage(marked.Data.maxHealth + 1f);
                Game.Expedition.ClearMarkedTarget();
                IsTrue(Game.Expedition.MarkedTarget == null, "目标死后标记没有清掉");

                // 关键：队友真的会改打标记的那个，而不是最近的那个。
                // 故意标记「离队友最远」的敌人，这样只要标记没生效，选到的就是别人。
                //
                // 刷怪点是随机的，直接取最近/最远有概率撞上距离相等 ——
                // 先把队友挪到某个敌人脚下，最近的就唯一确定了。
                var follower = Game.Expedition.Squad[0];
                var alive = AliveEnemies();
                IsTrue(alive.Count >= 2, "这个用例需要至少两个活着的敌人");

                follower.TeleportTo(StoreGrid.WorldToCell(alive[0].Position));

                var nearest = NearestAliveEnemyFrom(follower);
                var farthest = FarthestAliveEnemyFrom(follower);
                IsTrue(nearest == alive[0], "站到敌人脚下之后，最近的应该就是它");
                IsTrue(farthest != nearest, "最远和最近必须是两个不同的敌人");

                Game.Expedition.SetMarkedTarget(farthest);
                IsTrue(Game.Expedition.MarkedTarget == farthest, "手动指定标记失败");
                IsTrue(follower.SelectTarget() == farthest,
                       "队友没有优先打标记的目标，还是去打最近的了");

                Game.Expedition.ClearMarkedTarget();
                IsTrue(follower.SelectTarget() == nearest,
                       "取消标记后应该回到「打最近的」");
            }
            finally
            {
                Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }

            IsTrue(Game.Expedition.MarkedTarget == null, "收队后标记应该被清空");
        });

        // ------------------------------------------------------------------
        // 多房间 — 设计文档 §3.4 / §11.1
        // ------------------------------------------------------------------

        /// <summary>§3.4 列了六类房间；§18 第二阶段的「5 个房间」指营地之后那 5 间。</summary>
        static void Test_TwilightForestRouteShape()
        {
            var route = GameDatabase.TwilightForest;
            IsTrue(route.Count >= 2, "暮光森林至少要有营地和一个房间");

            AreEqual((int)RoomKind.Camp, (int)route[0].kind, "第一间必须是入口营地");
            AreEqual(5, route.Count - 1, "§18 第二阶段：营地之后应该有 5 个房间");
            AreEqual((int)RoomKind.Boss, (int)route[route.Count - 1].kind, "最后一间必须是 Boss 房");

            // §3.4 的六类房间要各出现至少一次
            var kinds = new HashSet<RoomKind>();
            for (int i = 0; i < route.Count; i++)
            {
                var room = route[i];
                IsTrue(!string.IsNullOrEmpty(room.roomId), $"第 {i} 间房没有 id");
                IsTrue(!string.IsNullOrEmpty(room.displayName), $"第 {i} 间房没有名字");
                IsTrue(!string.IsNullOrEmpty(room.briefing), $"{room.displayName} 没有说明文案");

                if (room.HasEnemies)
                    IsTrue(GameDatabase.GetEnemy(room.enemyId) != null,
                           $"{room.displayName} 引用了不存在的敌人 {room.enemyId}");

                if (room.HasMinions)
                    IsTrue(GameDatabase.GetEnemy(room.minionEnemyId) != null,
                           $"{room.displayName} 引用了不存在的杂兵 {room.minionEnemyId}");

                kinds.Add(room.kind);
            }

            foreach (RoomKind kind in System.Enum.GetValues(typeof(RoomKind)))
                IsTrue(kinds.Contains(kind), $"路线里缺少 {kind} 类型的房间（§3.4 列了六类）");

            // 精英房和 Boss 房必须真的放精英和 Boss ——
            // 这两间以前是拿普通跳跳菇占位的，别再退回去
            AreEqual((int)EnemyTier.Elite, (int)RoomTierOf(route, RoomKind.Elite),
                     "精英空地放的不是精英（§3.4 精英房）");
            AreEqual((int)EnemyTier.Boss, (int)RoomTierOf(route, RoomKind.Boss),
                     "Boss 空地放的不是区域 Boss（§3.5 孢子巨兽）");
        }

        static EnemyTier RoomTierOf(IReadOnlyList<RoomData> route, RoomKind kind)
        {
            for (int i = 0; i < route.Count; i++)
            {
                if (route[i].kind != kind) continue;

                var enemy = GameDatabase.GetEnemy(route[i].enemyId);
                IsTrue(enemy != null, $"{route[i].displayName} 没有配敌人");
                return enemy.tier;
            }
            throw new Exception($"路线里没有 {kind} 房");
        }

        /// <summary>清场之前传送点不放行；清完才能走下一间。</summary>
        static void Test_ExitPortalGatesOnClear() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Battle);

                int battleIndex = Game.Expedition.RoomIndex;
                IsTrue(!Game.Expedition.RoomCleared, "战斗房一进去不该是已清场");

                // 没清场就想走 → 应该原地不动
                Game.Expedition.AdvanceRoom();
                AreEqual(battleIndex, Game.Expedition.RoomIndex,
                         "没清场却被放行到下一间了");

                KillAllEnemies();
                IsTrue(Game.Expedition.RoomCleared, "敌人清光了却没判成已清场");

                Game.Expedition.AdvanceRoom();
                AreEqual(battleIndex + 1, Game.Expedition.RoomIndex, "清场后没能进下一间");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// 换房间时：队长和员工是同一批对象（血量、技能冷却跨房间保留），
        /// 而地形、敌人和地上的掉落物按新房间重建。
        /// </summary>
        static void Test_RoomSwitchKeepsSquadRebuildsRoom() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Battle);

                var captainBefore = Game.Expedition.Captain;
                var followerBefore = Game.Expedition.Squad[0];
                var worldBefore = Game.Expedition.World;

                // 让队长挂点彩，验证血量不会因为换房间被重置
                captainBefore.Health.Damage(20f);
                float hpBefore = captainBefore.Health.Current;
                IsTrue(hpBefore < captainBefore.Health.Max, "前置条件：队长应该已经掉血");

                KillAllEnemies();
                IsTrue(Game.Expedition.LootOnGround > 0, "战斗房清场后地上应该有掉落物");

                Game.Expedition.AdvanceRoom();

                IsTrue(Game.Expedition.Captain == captainBefore, "换房间后队长被重建了");
                IsTrue(Game.Expedition.Squad[0] == followerBefore, "换房间后队友被重建了");
                AreEqualFloat(hpBefore, Game.Expedition.Captain.Health.Current,
                              "换房间把队长的血量重置了");

                IsTrue(Game.Expedition.World != worldBefore, "换房间后地形没有重建");
                AreEqual(0, Game.Expedition.LootOnGround, "上一间没捡的掉落物应该随房间一起清掉");

                var room = Game.Expedition.CurrentRoom;
                AreEqual(room.TotalEnemyCount, Game.Expedition.EnemiesRemaining,
                         $"「{room.displayName}」的敌人数没有按房间数据重建");

                // 小队落点必须在新房间的可行走区
                var grid = Game.Expedition.World.Grid;
                IsTrue(grid.IsWalkable(Game.Expedition.Captain.Cell), "换房间后队长落在墙里");
                for (int i = 0; i < Game.Expedition.Squad.Count; i++)
                    IsTrue(grid.IsWalkable(Game.Expedition.Squad[i].Cell), "换房间后队友落在墙里");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        static void Test_LastRoomFinishesExpedition() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();

            AdvanceToRoom(RoomKind.Boss);
            IsTrue(Game.Expedition.IsLastRoom, "Boss 房应该是最后一间");
            IsTrue(Game.Expedition.IsRunning, "还没走完就结束了");

            KillAllEnemies();
            Game.Expedition.AdvanceRoom();

            IsTrue(!Game.Expedition.IsRunning, "走完最后一间应该收队");
            AreEqual((int)ExpeditionOutcome.Cleared, (int)Game.Expedition.Outcome, "收队结果");
            AreEqual((int)GameState.Preparation, (int)Game.Manager.State, "收队后应该回到备货阶段");
        });

        // ------------------------------------------------------------------
        // 采集与事件房 — 设计文档 §3.2 / §3.4
        // ------------------------------------------------------------------

        static void Test_HarvestNodeFillsBag() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Resource);

                var room = Game.Expedition.CurrentRoom;
                IsTrue(room.HasHarvest, "资源房应该配了采集点");
                AreEqual(room.harvestProductIds.Count, Game.Expedition.HarvestNodes.Count,
                         "采集点数量没有按房间数据生成");

                var nodes = Game.Expedition.HarvestNodes;

                // 采集点之间必须隔开一个采集半径以上。
                // 挨在一起的话，站在其中一个上面按 E 会随机收到另一个 ——
                // 玩家没法指定采哪一堆，下面「采完就采不到了」的判定也会时红时绿。
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        float gap = (nodes[i].Position - nodes[j].Position).magnitude;
                        IsTrue(gap > HarvestNode.HarvestRadius * 2f,
                               $"第 {i} 和第 {j} 个采集点挨得太近（相距 {gap:0.00}），" +
                               "站在一个上面会连着采到另一个");
                    }
                }

                var node = nodes[0];
                IsTrue(node.Remaining > 0, "采集点一开始应该有货");
                IsTrue(GameDatabase.GetProduct(node.Product.productId) != null,
                       "采集点产出的商品不在商品表里");

                // 站到采集点上再采 —— 不站过去就不该采得到
                AreEqual(0, Game.Expedition.HarvestInReach(), "离得远也能采到，范围判定没生效");

                int before = Game.Expedition.BagCount;
                int stock = node.Remaining;

                Game.Expedition.Captain.TeleportTo(StoreGrid.WorldToCell(node.Position));
                int taken = Game.Expedition.HarvestInReach();

                AreEqual(stock, taken, "站上去应该一次把这个采集点收完");
                AreEqual(before + stock, Game.Expedition.BagCount, "采到的货没进背包");
                IsTrue(node.IsEmpty, "采完之后采集点应该空了");
                AreEqual(0, Game.Expedition.HarvestInReach(), "空掉的采集点还能继续采");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// 回归用例。
        ///
        /// 采集点的落点是随机的，原来只做了「不在同一格」的去重。
        /// 两个采集点隔一格时，两边都落在对方 1.2 格的采集半径内：
        /// 站在其中一个上面按 E 会连着把另一个也收掉，
        /// 「路线与携带容量」的取舍（§3.4 资源房）就消失了。
        ///
        /// 这个缺陷是概率性的（一趟大约 6% 会撞上），单看一趟测不出来 ——
        /// 所以这里连开 12 趟，每趟都检查一遍。
        /// </summary>
        static void Test_HarvestNodesNeverOverlap() => WithIsolatedSaveFile(() =>
        {
            const int Rounds = 12;

            for (int round = 0; round < Rounds; round++)
            {
                Game.Expedition.Begin();
                try
                {
                    AdvanceToRoom(RoomKind.Resource);

                    var nodes = Game.Expedition.HarvestNodes;
                    AreEqual(Game.Expedition.CurrentRoom.harvestProductIds.Count, nodes.Count,
                             $"第 {round + 1} 趟：采集点没有按房间数据刷全");

                    for (int i = 0; i < nodes.Count; i++)
                    {
                        for (int j = i + 1; j < nodes.Count; j++)
                        {
                            float gap = (nodes[i].Position - nodes[j].Position).magnitude;
                            IsTrue(gap > HarvestNode.HarvestRadius * 2f,
                                   $"第 {round + 1} 趟：{nodes[i].Product.displayName} 和 " +
                                   $"{nodes[j].Product.displayName} 的采集点相距只有 {gap:0.00} 格，" +
                                   "站在一个上面会连着采到另一个");
                        }
                    }
                }
                finally
                {
                    if (Game.Expedition.IsRunning)
                        Game.Expedition.Finish(ExpeditionOutcome.Retreated);
                }
            }
        });

        /// <summary>
        /// §3.2 / §12.2 都把携带容量列为远征的关键约束，
        /// §3.3 的取舍「保留订单商品、畅销商品还是稀有升级材料」正建立在它之上。
        /// </summary>
        static void Test_BagCapacityLimitsHarvest() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Resource);

                var soda = GameDatabase.GetProduct("blood_orange_soda");

                // 先把背包塞到只剩 1 格
                int added = Game.Expedition.AddToBag(soda, ExpeditionManager.BagCapacity - 1);
                AreEqual(ExpeditionManager.BagCapacity - 1, added, "填充背包时被意外截断");
                AreEqual(1, Game.Expedition.BagSpaceLeft, "背包应该只剩 1 格");

                var node = Game.Expedition.HarvestNodes[0];
                int stock = node.Remaining;
                IsTrue(stock >= 2, "这个用例需要采集点里不止 1 件");

                Game.Expedition.Captain.TeleportTo(StoreGrid.WorldToCell(node.Position));
                int taken = Game.Expedition.HarvestInReach();

                AreEqual(1, taken, "背包只剩 1 格，却采了不止 1 件");
                AreEqual(ExpeditionManager.BagCapacity, Game.Expedition.BagCount, "背包超载了");
                AreEqual(stock - 1, node.Remaining, "装不下的那部分应该留在采集点上");
                IsTrue(Game.Expedition.BagFull, "背包该判为已满");

                // 满了之后再采应该颗粒无收
                AreEqual(0, Game.Expedition.HarvestInReach(), "背包满了还能继续采");

                // 直接往背包里塞也必须被容量挡住 —— HarvestInReach 自己也会按
                // BagSpaceLeft 削一刀，只测采集的话这条边界会被那层遮住。
                AreEqual(0, Game.Expedition.AddToBag(soda, 50), "背包满了还能塞进去");
                AreEqual(ExpeditionManager.BagCapacity, Game.Expedition.BagCount,
                         "背包被塞超载了");

                // 半满时请求超量，只应该收下剩余空间那么多
                Game.Expedition.Finish(ExpeditionOutcome.Retreated);
                Game.Expedition.Begin();

                AreEqual(0, Game.Expedition.BagCount, "新一趟远征背包应该是空的");
                AreEqual(ExpeditionManager.BagCapacity,
                         Game.Expedition.AddToBag(soda, ExpeditionManager.BagCapacity + 30),
                         "超量请求应该只收下容量上限那么多");
                AreEqual(ExpeditionManager.BagCapacity, Game.Expedition.BagCount, "背包超载了");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        static ExpeditionEventOption FindEventOption(string eventId, ExpeditionEventEffect effect)
        {
            var data = GameDatabase.GetExpeditionEvent(eventId);
            IsTrue(data != null, $"找不到事件 {eventId}");

            for (int i = 0; i < data.options.Count; i++)
                if (data.options[i].effect == effect) return data.options[i];

            throw new Exception($"事件 {eventId} 里没有 {effect} 选项");
        }

        static void Test_EventTradeRequiresCoins() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Event);
                IsTrue(Game.Expedition.CurrentRoom.HasEvent, "事件房应该配了事件");

                var trade = FindEventOption(Game.Expedition.CurrentRoom.eventId,
                                            ExpeditionEventEffect.Trade);
                var product = GameDatabase.GetProduct(trade.productId);

                // 钱不够 → 不生效，货和钱都不动
                Game.Economy.SetMoney(trade.coinCost - 1);
                int bagBefore = Game.Expedition.BagCount;

                IsTrue(!Game.Expedition.ApplyEventOption(trade), "钱不够却交易成功了");
                AreEqual(trade.coinCost - 1, Game.Economy.Money, "交易失败却扣了钱");
                AreEqual(bagBefore, Game.Expedition.BagCount, "交易失败却拿到了货");

                // 钱够 → 扣钱给货
                Game.Economy.SetMoney(trade.coinCost + 10);
                IsTrue(Game.Expedition.ApplyEventOption(trade), "钱够了却交易失败");
                AreEqual(10, Game.Economy.Money, "交易没有按标价扣钱");
                AreEqual(bagBefore + trade.productCount, Game.Expedition.BagCount,
                         $"没拿到 {product.displayName}");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        static void Test_EventScavengeCostsHealth() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Event);

                var scavenge = FindEventOption(Game.Expedition.CurrentRoom.eventId,
                                               ExpeditionEventEffect.Scavenge);
                IsTrue(scavenge.squadDamage > 0f, "搜刮必须有代价，否则就是白送");

                int bagBefore = Game.Expedition.BagCount;
                int coinsBefore = Game.Economy.Money;

                var captain = Game.Expedition.Captain;
                float captainHp = captain.Health.Current;

                var squad = Game.Expedition.Squad;
                var squadHp = new List<float>();
                for (int i = 0; i < squad.Count; i++) squadHp.Add(squad[i].Health.Current);

                IsTrue(Game.Expedition.ApplyEventOption(scavenge), "搜刮应该总能成功");

                AreEqual(coinsBefore, Game.Economy.Money, "搜刮不该花钱");
                AreEqual(bagBefore + scavenge.productCount, Game.Expedition.BagCount, "搜刮没拿到货");

                AreEqualFloat(captainHp - scavenge.squadDamage, captain.Health.Current,
                              "队长没有付出代价");
                for (int i = 0; i < squad.Count; i++)
                    AreEqualFloat(squadHp[i] - scavenge.squadDamage, squad[i].Health.Current,
                                  $"{squad[i].Data.displayName} 没有付出代价");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        // ------------------------------------------------------------------
        // 精英与区域 Boss — 设计文档 §3.3 / §3.4 / §3.5
        // ------------------------------------------------------------------

        /// <summary>
        /// §1.5 原型规模写的是「3 种普通敌人 + 1 个区域 Boss」，
        /// §3.5 点名了跳跳菇、刺藤精、森林盗贼和孢子巨兽。
        /// 精英与 Boss 曾经是拿普通跳跳菇占位的，这条用例盯着别退回去。
        /// </summary>
        static void Test_EnemyRosterCoversAllTiers()
        {
            var enemies = GameDatabase.Enemies;
            int normal = 0, elite = 0, boss = 0;
            float strongestNormal = 0f;

            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                IsTrue(!string.IsNullOrEmpty(e.enemyId), $"第 {i} 个敌人没有 id");
                IsTrue(!string.IsNullOrEmpty(e.displayName), $"{e.enemyId} 没有名字");
                IsTrue(e.maxHealth > 0f && e.moveSpeed > 0f, $"{e.displayName} 的数值非法");
                IsTrue(e.telegraphSeconds > 0f,
                       $"{e.displayName} 没有攻击前摇 —— §3.3 要求敌人攻击必须有清晰前摇");
                IsTrue(GameDatabase.GetProduct(e.lootProductId) != null,
                       $"{e.displayName} 掉落的 {e.lootProductId} 不在商品表里");

                switch (e.tier)
                {
                    case EnemyTier.Normal:
                        normal++;
                        strongestNormal = Mathf.Max(strongestNormal, e.maxHealth);
                        break;
                    case EnemyTier.Elite: elite++; break;
                    case EnemyTier.Boss: boss++; break;
                }
            }

            AreEqual(3, normal, "普通敌人数量（§1.5「3 种普通敌人」/ §3.5 点名三只）");
            AreEqual(1, elite, "精英数量（§3.4 精英房）");
            AreEqual(1, boss, "区域 Boss 数量（§1.5「1 个区域 Boss」）");

            var guardian = GameDatabase.GetEnemy(GameDatabase.EliteEnemyId);
            IsTrue(guardian != null && guardian.tier == EnemyTier.Elite, "找不到精英");
            IsTrue(guardian.basicAttackResist > 0f && guardian.basicAttackResist < 1f,
                   "精英的护甲比例非法 —— 0 等于没有护甲，1 等于普攻完全无效");
            IsTrue(guardian.maxHealth > strongestNormal,
                   "精英比最硬的普通敌人还脆，§3.4「风险较高」立不住");

            var behemoth = GameDatabase.GetEnemy(GameDatabase.BossEnemyId);
            IsTrue(behemoth != null && behemoth.tier == EnemyTier.Boss, "找不到区域 Boss");
            IsTrue(behemoth.maxHealth > guardian.maxHealth, "Boss 比精英还脆");
            IsTrue(behemoth.UsesSporeVents,
                   "Boss 没有区域机制 —— §3.3 要求「Boss 通过区域机制、护送商品或关闭装置制造变化」");
            IsTrue(behemoth.ventReopenSeconds > 0f, "破防窗口没有长度");
            IsTrue(behemoth.ventPulseDamage > 0f, "喷口不掉血，关装置就没有代价");
            IsTrue(behemoth.shieldedDamageMultiplier < 1f, "护盾不减伤");
            IsTrue(behemoth.coldShelfCores > 0,
                   "Boss 不掉冷藏货架核心 —— §3.5「击败后获得冷藏货架核心」");
            IsTrue(!string.IsNullOrEmpty(behemoth.unlocksRegionId),
                   "Boss 不解锁下一地区 —— §3.4「掉落关键设施材料并解锁下一地区」");
        }

        /// <summary>
        /// §3.4「精英房：风险较高」的玩法本体：小队的自动普攻磨不动精英，
        /// 玩家必须挑时机按 1~3 放技能（§3.3「玩家负责……主动技能时机」）。
        /// 护甲要是也挡技能，精英就只是个血包。
        /// </summary>
        static void Test_EliteArmorOnlyBlocksBasicAttacks() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Elite);

                var elite = FirstEnemyOfTier(EnemyTier.Elite);
                IsTrue(elite != null, "精英房里没有精英");
                IsTrue(elite.Data.basicAttackResist > 0f, "前置条件：精英应该有护甲");

                const float Punch = 40f;

                float hpBefore = elite.Health.Current;
                float basic = elite.TakeDamage(Punch, DamageKind.Basic);
                AreEqualFloat(Punch * (1f - elite.Data.basicAttackResist), basic,
                              "精英护甲对普通攻击的减伤");
                AreEqualFloat(hpBefore - basic, elite.Health.Current,
                              "结算出来的伤害和实际扣血对不上");

                hpBefore = elite.Health.Current;
                float skill = elite.TakeDamage(Punch, DamageKind.Skill);
                AreEqualFloat(Punch, skill, "技能被精英护甲削弱了 —— 那就没有理由攒技能了");
                AreEqualFloat(hpBefore - Punch, elite.Health.Current, "技能的扣血对不上");

                IsTrue(skill > basic, "技能和普攻打进去一样多，护甲形同虚设");

                // 对照：同一间房里的杂兵没有护甲，普攻必须打满 ——
                // 否则上面那条可能只是「所有伤害都打了折」
                var minion = FirstEnemyOfTier(EnemyTier.Normal);
                IsTrue(minion != null, "精英房应该还有杂兵当对照");
                AreEqualFloat(Punch, minion.TakeDamage(Punch, DamageKind.Basic),
                              "普通敌人不该有护甲");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>精英房 = 一只精英 + 若干杂兵，先清杂兵还是先集火是这间房的取舍。</summary>
        static void Test_EliteRoomSpawnsGuardianAndMinions() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Elite);

                var room = Game.Expedition.CurrentRoom;
                IsTrue(room.HasMinions, "精英房应该配杂兵，否则就是纯单挑");
                IsTrue(room.TotalEnemyCount > room.enemyCount, "杂兵没有算进这间房的总敌人数");

                AreEqual(room.TotalEnemyCount, Game.Expedition.EnemiesRemaining,
                         "精英房的敌人没有按房间数据刷全");
                AreEqual(room.enemyCount, CountAliveOfTier(EnemyTier.Elite), "精英数量");
                AreEqual(room.minionCount, CountAliveOfTier(EnemyTier.Normal), "杂兵数量");

                // 清场判定要把杂兵也算进去，不能打死精英就放行
                var elite = FirstEnemyOfTier(EnemyTier.Elite);
                elite.Health.Damage(elite.Data.maxHealth + 1f);
                IsTrue(!Game.Expedition.RoomCleared, "杂兵还活着就判成清场了");

                KillAllEnemies();
                IsTrue(Game.Expedition.RoomCleared, "全清了却没判成清场");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>§4.2 吸血鬼·维拉的远征功能是「对精英怪额外伤害」。</summary>
        static void Test_VeraHitsElitesHarder() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin("vampire_vera", "slime_bobo", "ghost_mia");
            try
            {
                AdvanceToRoom(RoomKind.Elite);

                var vera = Game.Expedition.Squad[0];
                AreEqual("vampire_vera", vera.Data.staffId, "队伍第一位应该是维拉");
                IsTrue(vera.Data.eliteDamageMultiplier > 1f,
                       "维拉的「对精英怪额外伤害」没有落成数值（§4.2）");

                var elite = FirstEnemyOfTier(EnemyTier.Elite);
                var minion = FirstEnemyOfTier(EnemyTier.Normal);
                IsTrue(elite != null && minion != null, "这个用例需要精英和杂兵各一只");

                const float Base = 10f;

                AreEqualFloat(Base * vera.Data.eliteDamageMultiplier,
                              vera.OutgoingDamage(Base, elite), "维拉对精英的伤害");
                AreEqualFloat(Base, vera.OutgoingDamage(Base, minion),
                              "维拉对普通敌人不该有加成");

                // 对照：别人没有这条被动，否则「带不带维拉」就没有区别
                var bobo = Game.Expedition.Squad[1];
                AreEqualFloat(1f, bobo.Data.eliteDamageMultiplier,
                             $"{bobo.Data.displayName} 不该有对精英加成");
                AreEqualFloat(Base, bobo.OutgoingDamage(Base, elite),
                             $"{bobo.Data.displayName} 打精英也吃到了加成");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// 用户反馈明确要求「队长也能打，不需要站在那里不动」——手动技能、有冷却、
        /// 不吃 MP，伤害高到直接秒杀范围内的敌人。
        /// </summary>
        static void Test_CaptainSkillOneShotsNonBossAndHasCooldown() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Resource);

                var captain = Game.Expedition.Captain;
                var enemy = FirstAliveEnemy();
                IsTrue(enemy != null, "资源房应该有一只普通敌人");

                captain.TeleportTo(enemy.Cell);
                IsTrue(captain.SkillReady, "前置条件：技能一开始应该没有冷却");

                IsTrue(captain.TryUseSkill(), "冷却好的时候应该能放技能");
                IsTrue(!enemy.IsAlive, "范围内的敌人应该被秒杀");
                IsTrue(!captain.SkillReady, "放完技能应该立刻进入冷却");
                IsTrue(!captain.TryUseSkill(), "冷却没到还能再放一次");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>Boss 战还是得靠关喷口——队长这个大招不能绕过区域机制直接秒了 Boss。</summary>
        static void Test_CaptainSkillSparesBoss() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Boss);

                var captain = Game.Expedition.Captain;
                var boss = Game.Expedition.Boss;
                IsTrue(boss != null, "Boss 房里没有 Boss");

                captain.TeleportTo(boss.Cell);
                float hpBefore = boss.Health.Current;

                IsTrue(captain.TryUseSkill(), "冷却好的时候应该能放技能");
                AreEqualFloat(hpBefore, boss.Health.Current,
                              "队长技能不该伤到 Boss —— Boss 战还是得靠关喷口，不能被这个技能绕过去");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// §3.3 要求「Boss 通过区域机制、护送商品或<b>关闭装置</b>制造变化」。
        /// 孢子巨兽选的是关闭装置：喷口全开时它几乎无敌，队长必须跑一圈把喷口关掉。
        /// </summary>
        static void Test_BossShieldHoldsUntilVentsClosed() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Boss);

                var boss = Game.Expedition.Boss;
                IsTrue(boss != null, "Boss 房里没有 Boss");
                IsTrue(boss.IsBoss, "Boss 房里站的不是 Boss 分级的敌人");

                AreEqual(boss.Data.ventCount, Game.Expedition.Vents.Count, "孢子喷口数量");
                AreEqual(boss.Data.ventCount, Game.Expedition.OpenVentCount,
                         "一进 Boss 房喷口就该全开着");
                IsTrue(boss.IsShielded, "喷口开着，Boss 却没有护盾");

                const float Punch = 100f;

                float shielded = boss.TakeDamage(Punch, DamageKind.Skill);
                AreEqualFloat(Punch * boss.Data.shieldedDamageMultiplier, shielded, "护盾减伤");
                IsTrue(shielded < Punch * 0.5f,
                       "护盾几乎没减伤，玩家大可无视喷口硬打，区域机制就没意义了");

                // 站得远关不掉 —— 否则「跑一圈关装置」这件事根本不存在
                IsTrue(Game.Expedition.VentInReach() == null, "离喷口很远却判成够得着");
                IsTrue(!Game.Expedition.CloseVentInReach(), "离得远也能关喷口");
                AreEqual(boss.Data.ventCount, Game.Expedition.OpenVentCount, "喷口被凭空关掉了");

                CloseAllVentsByWalking();

                AreEqual(0, Game.Expedition.OpenVentCount, "喷口没关完");
                IsTrue(Game.Expedition.VentsAllClosed, "全关了却没判成破防");
                IsTrue(!boss.IsShielded, "喷口全关了 Boss 还带着护盾");

                float exposed = boss.TakeDamage(Punch, DamageKind.Skill);
                AreEqualFloat(Punch, exposed, "破防之后伤害还是被打了折");
                IsTrue(exposed > shielded, "关不关装置打进去的一样多，机制没生效");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>开着的喷口必须有代价，否则玩家可以站着不动慢慢磨。</summary>
        static void Test_OpenVentsBurnNearbySquad() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Boss);

                var vent = Game.Expedition.Vents[0];
                IsTrue(vent.IsOpen, "喷口一进场应该是开着的");
                IsTrue(vent.PulseDamage > 0f, "喷口不掉血就不是代价");

                var captain = Game.Expedition.Captain;
                captain.TeleportTo(StoreGrid.WorldToCell(vent.Position));

                // 对照组：挪到喷口范围外，验证的是「范围」而不是「全场挨打」
                var outside = Game.Expedition.Squad[0];
                outside.TeleportTo(Game.Expedition.World.CampCell);
                IsTrue((outside.Position - vent.Position).magnitude > vent.PulseRadius,
                       "前置条件不成立：对照组没有挪出喷口范围");

                float hpInside = captain.Health.Current;
                float hpOutside = outside.Health.Current;

                // 没到间隔不该喷
                IsTrue(!vent.TickPulse(vent.PulseCountdown * 0.5f), "还没到间隔就喷了");
                AreEqualFloat(hpInside, captain.Health.Current, "还没到间隔却已经掉血");

                IsTrue(vent.TickPulse(vent.PulseCountdown + 0.01f), "到了间隔却没喷");
                AreEqualFloat(hpInside - vent.PulseDamage, captain.Health.Current,
                              "站在喷口上没有被灼伤");
                AreEqualFloat(hpOutside, outside.Health.Current,
                              "喷口范围外的队友也被灼伤了");

                // 关掉之后必须停手
                vent.Close();
                float hpAfterClose = captain.Health.Current;
                IsTrue(!vent.TickPulse(999f), "关掉的喷口还在喷");
                AreEqualFloat(hpAfterClose, captain.Health.Current, "关掉的喷口还在灼伤小队");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// 破防是<b>窗口</b>不是买断：关完一轮之后喷口会重新喷发，
        /// 整场 Boss 战才会变成「关装置 → 集火 → 再关」的循环（§3.3「制造变化」）。
        /// </summary>
        static void Test_VentsReopenAfterWindow() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Boss);

                var boss = Game.Expedition.Boss;
                float window = boss.Data.ventReopenSeconds;
                IsTrue(window > 0f, "破防窗口必须有长度");

                CloseAllVentsByWalking();
                AreEqualFloat(window, Game.Expedition.VentReopenCountdown,
                              "关完喷口没有进入破防窗口");

                // 窗口没走完就不该重开
                Game.Expedition.TickBossArena(window * 0.5f);
                AreEqual(0, Game.Expedition.OpenVentCount, "破防窗口还没结束喷口就重开了");
                IsTrue(!boss.IsShielded, "破防窗口里 Boss 不该有护盾");

                // 窗口走完 → 全部重新喷发
                Game.Expedition.TickBossArena(window);
                AreEqual(boss.Data.ventCount, Game.Expedition.OpenVentCount,
                         "破防窗口结束后喷口没有重新喷发 —— 关一次就永久破防等于没有机制");
                IsTrue(boss.IsShielded, "喷口重开了，Boss 却没恢复护盾");

                // Boss 倒下之后就别再喷了，清完场还挨伤害只会让人莫名其妙
                KillAllEnemies();
                Game.Expedition.TickBossArena(window * 2f);
                AreEqual(0, Game.Expedition.OpenVentCount, "Boss 都死了喷口还在喷");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// §3.4「Boss 房：结算区域机制，掉落关键设施材料并解锁下一地区」，
        /// §3.5「区域 Boss：孢子巨兽；击败后获得冷藏货架核心」。
        /// </summary>
        static void Test_BossDropsColdShelfCoreAndUnlocksRegion() => WithIsolatedSaveFile(() =>
        {
            var bossData = GameDatabase.GetEnemy(GameDatabase.BossEnemyId);
            IsTrue(bossData.coldShelfCores > 0, "前置条件：Boss 应该掉冷藏货架核心");

            AreEqual(0, ExpeditionProgress.ColdShelfCores, "前置条件：一开始不该有核心");
            IsTrue(!ExpeditionProgress.IsRegionUnlocked(bossData.unlocksRegionId),
                   "前置条件：下一地区不该已经解锁");

            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Boss);

                AreEqual(0, ExpeditionProgress.ColdShelfCores,
                         "还没打死 Boss 就把奖励发了");

                KillAllEnemies();

                AreEqual(bossData.coldShelfCores, ExpeditionProgress.ColdShelfCores,
                         "击败 Boss 没拿到冷藏货架核心");
                IsTrue(ExpeditionProgress.IsRegionUnlocked(bossData.unlocksRegionId),
                       "击败 Boss 没有解锁下一地区");

                // 关键设施材料不是货：不占携带容量，也不该被 §3.7 的失败折损砍掉
                AreEqual(0, Game.Expedition.BagCount, "冷藏货架核心占用了携带容量");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Failed);
            }

            AreEqual(bossData.coldShelfCores, ExpeditionProgress.ColdShelfCores,
                     "被击退把已经打下来的关键设施材料也扣掉了");
        });

        /// <summary>
        /// 冷藏货架核心和地区解锁是<b>本局进度</b>，和金钱同级：
        /// 续玩要恢复，开新局要跟着一起丢。旧存档没有这两个键，读出来是 0 / 空。
        /// </summary>
        static void Test_ColdShelfCoreSurvivesSaveButNotNewRun() => WithIsolatedSaveFile(() =>
        {
            const string Region = "ash_volcano";

            ExpeditionProgress.AddColdShelfCores(2);
            ExpeditionProgress.UnlockRegion(Region);
            Game.Manager.ResetRunState(2, 40);
            SaveSystem.Save();

            var save = SaveSystem.Load();
            IsTrue(save != null, "存档没读回来");
            AreEqual(2, save.coldShelfCores, "存档里的冷藏货架核心");
            IsTrue(save.unlockedRegions != null && save.unlockedRegions.Contains(Region),
                   "存档里没有记下已解锁的地区");

            // 续玩 → 恢复
            ExpeditionProgress.Reset();
            AreEqual(0, ExpeditionProgress.ColdShelfCores, "前置条件：清空失败");

            SaveSystem.Apply(save, true);
            AreEqual(2, ExpeditionProgress.ColdShelfCores, "续玩后没有恢复冷藏货架核心");
            IsTrue(ExpeditionProgress.IsRegionUnlocked(Region), "续玩后没有恢复地区解锁");

            // 开新局 → 跟着金钱一起丢掉
            SaveSystem.Apply(save, false);
            AreEqual(0, ExpeditionProgress.ColdShelfCores, "开新局还带着上一局的冷藏货架核心");
            IsTrue(!ExpeditionProgress.IsRegionUnlocked(Region), "开新局还带着上一局的地区解锁");

            // 旧存档没有这两个键 —— 纯新增字段不 bump SaveVersion，必须照样能读
            string legacy =
                "{\n" +
                $"    \"version\": {GameConfig.SaveVersion},\n" +
                "    \"currentDay\": 2,\n" +
                "    \"money\": 118,\n" +
                "    \"reputation\": 47,\n" +
                "    \"unlockedProducts\": [],\n" +
                "    \"discoveredMonsters\": [],\n" +
                "    \"checkoutLevel\": 0,\n" +
                "    \"sfxVolume\": 0.55,\n" +
                "    \"musicVolume\": 0.22\n" +
                "}";
            File.WriteAllText(SaveSystem.FilePath, legacy);

            var old = SaveSystem.Load();
            IsTrue(old != null, "旧存档被拒收了");
            AreEqual(0, old.coldShelfCores, "旧存档缺这个键，该默认为 0");

            SaveSystem.Apply(old, true);
            AreEqual(0, ExpeditionProgress.ColdShelfCores, "旧存档恢复后凭空多出了核心");
        });

        // ------------------------------------------------------------------
        // 轻度肉鸽三选一 — 设计文档 §3.6
        // ------------------------------------------------------------------

        /// <summary>
        /// §3.6 的核心约束：「强化优先提供<b>收益与代价</b>，而不是无脑增加伤害」。
        /// 只要有一条是纯收益，玩家就没有取舍，三选一退化成「选最大的那个数」。
        /// </summary>
        static void Test_EveryBoonPairsBenefitWithCost()
        {
            var boons = GameDatabase.Boons;
            AreEqual(4, boons.Count, "强化池大小（§3.6 给了四个示例）");
            IsTrue(boons.Count >= GameDatabase.BoonChoiceCount,
                   "强化池比一次三选一还小，抽不满");

            var ids = new HashSet<string>();
            for (int i = 0; i < boons.Count; i++)
            {
                var b = boons[i];
                IsTrue(!string.IsNullOrEmpty(b.boonId), $"第 {i} 个强化没有 id");
                IsTrue(ids.Add(b.boonId), $"强化 id 重复：{b.boonId}");
                IsTrue(!string.IsNullOrEmpty(b.displayName), $"{b.boonId} 没有名字");
                IsTrue(!string.IsNullOrEmpty(b.benefit), $"{b.displayName} 没写收益");
                IsTrue(!string.IsNullOrEmpty(b.cost), $"{b.displayName} 没写代价");

                IsTrue(b.HasBenefit, $"{b.displayName} 的收益没有落成数值，只是一句文案");
                IsTrue(b.HasCost,
                       $"{b.displayName} 只有收益没有代价 —— §3.6 要求收益与代价成对");
            }
        }

        static void Test_BoonOffersAreTwoToThreeAndDistinct() => WithIsolatedSaveFile(() =>
        {
            // §3.6「每次远征出现 2～3 次临时强化」：路线上打勾的房间数就是这个次数
            var route = GameDatabase.TwilightForest;
            int offers = 0;
            for (int i = 0; i < route.Count; i++)
                if (route[i].offersBoon) offers++;

            IsTrue(offers >= 2 && offers <= 3,
                   $"一趟远征给了 {offers} 次三选一，§3.6 要求 2～3 次");

            Game.Expedition.Begin();
            try
            {
                var first = Game.Expedition.RollBoonChoices();
                AreEqual(GameDatabase.BoonChoiceCount, first.Count, "三选一没有给满三个");

                var seen = new HashSet<string>();
                for (int i = 0; i < first.Count; i++)
                    IsTrue(seen.Add(first[i].boonId),
                           $"同一次三选一里出现了两个「{first[i].displayName}」");

                var taken = first[0];
                IsTrue(Game.Expedition.TakeBoon(taken), "强化没拿到");
                IsTrue(Game.Expedition.HasBoon(taken.boonId), "拿到的强化没记下来");

                // 已经拿过的不该再出现在后面的候选里 —— 抽签带随机，多抽几轮
                for (int round = 0; round < 12; round++)
                {
                    var next = Game.Expedition.RollBoonChoices();
                    for (int i = 0; i < next.Count; i++)
                        IsTrue(next[i].boonId != taken.boonId,
                               $"已经拿过的「{taken.displayName}」又被抽出来了");
                }

                IsTrue(!Game.Expedition.TakeBoon(taken), "同一个强化被拿了第二次");
                AreEqual(1, Game.Expedition.Boons.Count, "强化列表里出现了重复项");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>§3.6「仅在本次远征生效」—— 强化不进存档，也不能带到下一趟。</summary>
        static void Test_BoonsExpireWithTheExpedition() => WithIsolatedSaveFile(() =>
        {
            var wholesale = GameDatabase.GetBoon("wholesale_contract");
            IsTrue(wholesale != null, "找不到批发契约");

            Game.Expedition.Begin();
            IsTrue(Game.Expedition.TakeBoon(wholesale), "强化没拿到");
            AreEqualFloat(wholesale.normalLootMultiplier, Game.Expedition.NormalLootMultiplier,
                          "强化拿了却没生效");
            Game.Expedition.Finish(ExpeditionOutcome.Retreated);

            // 下一趟从零开始
            Game.Expedition.Begin();
            try
            {
                AreEqual(0, Game.Expedition.Boons.Count, "上一趟的强化被带到了下一趟");
                IsTrue(!Game.Expedition.HasBoon(wholesale.boonId), "上一趟的强化还记着");
                AreEqualFloat(1f, Game.Expedition.NormalLootMultiplier, "上一趟的掉落加成还在生效");
                AreEqualFloat(1f, Game.Expedition.BossLootMultiplier, "上一趟的 Boss 掉落惩罚还在生效");
                AreEqualFloat(1f, Game.Expedition.SkillCooldownMultiplier, "上一趟的冷却缩减还在生效");
                AreEqualFloat(ExpeditionManager.BaseFailKeepRatio, Game.Expedition.FailKeepRatio,
                              "上一趟的保留率加成还在生效");
                AreEqualFloat(ExpeditionCaptain.PickupRadius, Game.Expedition.PickupRadius,
                              "上一趟的拾取范围加成还在生效");
            }
            finally
            {
                Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>§3.6「批发契约：普通商品掉落翻倍，但 Boss 奖励品质下降」。</summary>
        static void Test_WholesaleContractTradesBossLootForVolume() => WithIsolatedSaveFile(() =>
        {
            var wholesale = GameDatabase.GetBoon("wholesale_contract");
            IsTrue(wholesale.normalLootMultiplier > 1f && wholesale.bossLootMultiplier < 1f,
                   "前置条件：批发契约应该是「普通翻倍、Boss 缩水」");

            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Resource);
                IsTrue(Game.Expedition.TakeBoon(wholesale), "强化没拿到");

                // ---- 普通敌人：掉落翻倍 ----
                var mob = FirstAliveEnemy();
                IsTrue(mob != null, "资源房里应该有一只普通敌人");

                var mobData = mob.Data;
                var mobCell = mob.Cell;
                int bagBefore = Game.Expedition.BagCount;

                KillAllEnemies();
                Game.Expedition.Captain.TeleportTo(mobCell);
                Game.Expedition.TryPickupNear(Game.Expedition.Captain);

                int gained = Game.Expedition.BagCount - bagBefore;
                // 翻倍后的区间是 [min*2, max*2]，下界严格高于原始上界 ——
                // 少了这一条，「掉落没翻倍」也可能蒙混过关
                IsTrue(mobData.lootMin * 2 > mobData.lootMax,
                       "这个用例要求翻倍后的下界高于原始上界，否则断言区分不出来");
                IsTrue(gained >= mobData.lootMin * 2 && gained <= mobData.lootMax * 2,
                       $"普通掉落没有翻倍：拿到 {gained}，" +
                       $"翻倍后应该在 {mobData.lootMin * 2}~{mobData.lootMax * 2} 之间");

                // ---- Boss：奖励品质下降 ----
                AdvanceToRoom(RoomKind.Boss);

                var boss = Game.Expedition.Boss;
                var bossData = boss.Data;
                var bossCell = boss.Cell;
                bagBefore = Game.Expedition.BagCount;

                KillAllEnemies();
                Game.Expedition.Captain.TeleportTo(bossCell);
                Game.Expedition.TryPickupNear(Game.Expedition.Captain);

                int bossLoot = Game.Expedition.BagCount - bagBefore;
                int halvedMax = Mathf.RoundToInt(bossData.lootMax * wholesale.bossLootMultiplier);
                IsTrue(halvedMax < bossData.lootMin,
                       "这个用例要求打折后的上界低于原始下界，否则断言区分不出来");
                IsTrue(bossLoot > 0 && bossLoot <= halvedMax,
                       $"Boss 奖励没有缩水：拿到 {bossLoot}，打折后最多 {halvedMax}");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// §3.6「加班狂热：技能冷却缩短，但远征结束后获得额外疲劳」。
        /// 疲劳系统属 §4.4 / §18 第四阶段，这里的代价先用「施法自损」代替。
        /// </summary>
        static void Test_OvertimeFrenzyTradesHealthForCooldown() => WithIsolatedSaveFile(() =>
        {
            var overtime = GameDatabase.GetBoon("overtime_frenzy");
            IsTrue(overtime.skillCooldownMultiplier < 1f && overtime.skillSelfDamage > 0f,
                   "前置条件：加班狂热应该是「冷却缩短 + 自损」");

            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Battle);

                var member = Game.Expedition.Squad[0];
                float baseCooldown = member.Data.skillCooldown;
                AreEqualFloat(baseCooldown, member.EffectiveSkillCooldown,
                              "还没拿强化，冷却就已经不是原值了");

                IsTrue(Game.Expedition.TakeBoon(overtime), "强化没拿到");

                AreEqualFloat(baseCooldown * overtime.skillCooldownMultiplier,
                              member.EffectiveSkillCooldown, "技能冷却没有按倍率缩短");
                IsTrue(member.EffectiveSkillCooldown < baseCooldown, "冷却没有变短");

                float hpBefore = member.Health.Current;
                IsTrue(member.SkillReady, "出发时技能应该是就绪的");
                IsTrue(member.TryUseSkill(), "技能放不出来");

                AreEqualFloat(hpBefore - overtime.skillSelfDamage, member.Health.Current,
                              "加班狂热没有付出代价 —— 那就是纯收益了");
                AreEqualFloat(member.EffectiveSkillCooldown, member.SkillCooldownRemaining,
                              "冷却没有按缩短后的时长起算");
                IsTrue(!member.SkillReady, "刚放完技能还判成就绪");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>§3.6「史莱姆快递：扩大拾取范围，但史莱姆携带货物时攻击力下降」。</summary>
        static void Test_SlimeDeliveryTradesDamageForReach() => WithIsolatedSaveFile(() =>
        {
            var delivery = GameDatabase.GetBoon("slime_delivery");
            IsTrue(delivery.pickupRadiusMultiplier > 1f && delivery.slimeAttackMultiplier < 1f,
                   "前置条件：史莱姆快递应该是「范围变大 + 史莱姆变弱」");

            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Battle);

                var slime = Game.Expedition.Squad[0];
                AreEqual("slime_bobo", slime.Data.staffId, "默认队伍第一位应该是史莱姆");

                var other = Game.Expedition.Squad[1];
                IsTrue(other.Data.monsterType != MonsterType.Slime, "对照组不该也是史莱姆");

                var enemy = FirstAliveEnemy();
                IsTrue(enemy != null, "战斗房里应该有敌人");

                const float Base = 10f;
                float slimeBefore = slime.OutgoingDamage(Base, enemy);
                float otherBefore = other.OutgoingDamage(Base, enemy);

                AreEqualFloat(ExpeditionCaptain.PickupRadius, Game.Expedition.PickupRadius,
                              "还没拿强化，拾取半径就已经不是基线了");

                IsTrue(Game.Expedition.TakeBoon(delivery), "强化没拿到");

                AreEqualFloat(ExpeditionCaptain.PickupRadius * delivery.pickupRadiusMultiplier,
                              Game.Expedition.PickupRadius, "拾取范围没有按倍率扩大");
                IsTrue(Game.Expedition.PickupRadius > ExpeditionCaptain.PickupRadius,
                       "拾取范围没有变大");

                AreEqualFloat(slimeBefore * delivery.slimeAttackMultiplier,
                              slime.OutgoingDamage(Base, enemy),
                              "史莱姆没有付出攻击力的代价");
                AreEqualFloat(otherBefore, other.OutgoingDamage(Base, enemy),
                              $"代价落到了 {other.Data.displayName} 头上 —— 这条只针对史莱姆");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>§3.6「易碎品保险：易碎商品不会因受击损坏，但移动速度降低」。</summary>
        static void Test_FragileInsuranceTradesSpeedForLoot() => WithIsolatedSaveFile(() =>
        {
            var insurance = GameDatabase.GetBoon("fragile_insurance");
            IsTrue(insurance.failKeepRatioBonus > 0f && insurance.captainSpeedMultiplier < 1f,
                   "前置条件：易碎品保险应该是「保留更多 + 走得更慢」");

            var soda = GameDatabase.GetProduct("blood_orange_soda");
            const int Carried = 10;

            // ---- 对照：没有保险时，被击退只保留一半（§3.7）----
            int before = Game.Store.WarehouseCount(soda);

            Game.Expedition.Begin();
            AreEqualFloat(ExpeditionManager.BaseFailKeepRatio, Game.Expedition.FailKeepRatio,
                          "没拿保险时的保留率不是基线");
            Game.Expedition.AddToBag(soda, Carried);
            Game.Expedition.Finish(ExpeditionOutcome.Failed);

            AreEqual(before + Mathf.FloorToInt(Carried * ExpeditionManager.BaseFailKeepRatio),
                     Game.Store.WarehouseCount(soda),
                     "前置条件不成立：没保险时应该只保留一半");

            // ---- 拿上保险再来一趟 ----
            before = Game.Store.WarehouseCount(soda);

            Game.Expedition.Begin();

            var captain = Game.Expedition.Captain;
            AreEqualFloat(ExpeditionCaptain.WalkSpeed, captain.EffectiveWalkSpeed,
                          "还没拿强化，队长的速度就已经变了");

            IsTrue(Game.Expedition.TakeBoon(insurance), "强化没拿到");

            AreEqualFloat(ExpeditionCaptain.WalkSpeed * insurance.captainSpeedMultiplier,
                          captain.EffectiveWalkSpeed, "队长的移动速度没有按倍率下降");
            AreEqualFloat(ExpeditionCaptain.SprintSpeed * insurance.captainSpeedMultiplier,
                          captain.EffectiveSprintSpeed, "冲刺速度没有跟着下降");
            IsTrue(captain.EffectiveWalkSpeed < ExpeditionCaptain.WalkSpeed,
                   "保险没有让队长变慢 —— 那就是纯收益了");

            float keep = Game.Expedition.FailKeepRatio;
            IsTrue(keep > ExpeditionManager.BaseFailKeepRatio, "保险没有提高失败时的保留率");

            Game.Expedition.AddToBag(soda, Carried);
            Game.Expedition.Finish(ExpeditionOutcome.Failed);

            AreEqual(before + Mathf.FloorToInt(Carried * keep),
                     Game.Store.WarehouseCount(soda),
                     "被击退时保险没有保住更多商品");
        });

        // ------------------------------------------------------------------
        // 昼夜循环与双岗位 — 设计文档 §2.1 / §4
        // ------------------------------------------------------------------

        /// <summary>
        /// §2.1 的单日六阶段，原型落成五步：
        /// 晨会 → 远征 → 闭店准备 → 午夜营业 → 日结 → 下一天的晨会。
        ///
        /// 以前一天是从「备货」开始的，远征只是备货界面上一个能无限点的按钮，
        /// 「今晚缺什么 → 白天去补」这条因果根本没成立。
        /// </summary>
        static void Test_DayLoopRunsMorningToNextMorning() => WithIsolatedSaveFile(() =>
        {
            Game.Manager.ResetRunState(1, 0);
            Game.Manager.BeginNewDay();

            AreEqual((int)GameState.MorningBrief, (int)Game.Manager.State,
                     "一天应该从晨会开始（§2.1 阶段一）");
            IsTrue(!Game.Manager.ExpeditionDoneToday, "新的一天远征次数应该是满的");

            // 阶段二：白天异世界进货
            IsTrue(Game.Manager.StartDayExpedition(), "晨会点了出发却没出发");
            AreEqual((int)GameState.Expedition, (int)Game.Manager.State, "出发后的状态");

            Game.Expedition.Finish(ExpeditionOutcome.Retreated);

            // 阶段三：闭店准备
            AreEqual((int)GameState.Preparation, (int)Game.Manager.State,
                     "远征回来应该进闭店准备");
            IsTrue(Game.Manager.ExpeditionDoneToday, "远征回来没记成今天已经去过");

            // 阶段四：午夜营业
            Game.Manager.OpenStore();
            AreEqual((int)GameState.Open, (int)Game.Manager.State, "营业中的状态");

            // 阶段五：日结
            var summary = Game.Manager.ConcludeDay();
            AreEqual((int)GameState.Settlement, (int)Game.Manager.State, "结算中的状态");
            AreEqual(1, summary.day, "结算的应该是第一天");

            // 回到下一天的晨会
            Game.Manager.ContinueAfterSettlement();

            AreEqual(2, Game.Day.CurrentDay, "没有进入第二天");
            AreEqual((int)GameState.MorningBrief, (int)Game.Manager.State,
                     "第二天应该也从晨会开始");
            IsTrue(!Game.Manager.ExpeditionDoneToday, "新的一天远征次数没有恢复");
        });

        static void Test_OnlyOneExpeditionPerDay() => WithIsolatedSaveFile(() =>
        {
            Game.Manager.ResetRunState(1, 0);
            Game.Manager.BeginNewDay();

            IsTrue(Game.Manager.StartDayExpedition(), "第一趟就没出发");
            Game.Expedition.Finish(ExpeditionOutcome.Retreated);

            IsTrue(!Game.Manager.StartDayExpedition(), "同一天出了第二趟远征");
            IsTrue(!Game.Expedition.IsRunning, "第二趟远征真的开起来了");

            // 「今天不出门」也算用掉了这一趟 —— 那是一个决定，不是跳过
            Game.Manager.BeginNewDay();
            IsTrue(!Game.Manager.ExpeditionDoneToday, "新的一天应该重置");

            Game.Manager.SkipExpedition();

            IsTrue(Game.Manager.ExpeditionDoneToday, "选了不出门却没算用掉");
            AreEqual((int)GameState.Preparation, (int)Game.Manager.State,
                     "不出门应该直接进闭店准备");
            IsTrue(!Game.Manager.StartDayExpedition(), "选了不出门之后又出门了");

            // 一个人都不派时不许出发 —— 否则会开出一支空队
            Game.Manager.BeginNewDay();
            var squad = StaffRoster.ExpeditionSquad();
            for (int i = 0; i < squad.Length; i++)
                StaffRoster.SetOnExpedition(squad[i], false);

            AreEqual(0, StaffRoster.SquadSize, "前置条件：应该没人出征");
            IsTrue(!Game.Manager.StartDayExpedition(), "没派人也能出发");
            IsTrue(!Game.Manager.ExpeditionDoneToday, "没出成的远征不该算用掉");
        });

        /// <summary>
        /// 用户明确要求「给远征模式开一个单独的入口，不需要白天打怪晚上看店，
        /// 可以纯一直打怪通关」——不用先走晨会排班，没排班也能用默认队伍打，
        /// 打完一趟能立刻再来一趟，不受「一天一趟远征」的限制。
        /// </summary>
        static void Test_ExpeditionOnlyModeLoopsWithoutDayCycle() => WithIsolatedSaveFile(() =>
        {
            Game.Manager.ResetRunState(1, 0);
            // 故意不走 BeginNewDay/晨会排班，模拟「直接从暂停菜单进纯远征模式」

            Game.Manager.EnterExpeditionOnlyMode();
            IsTrue(Game.Manager.ExpeditionOnlyMode, "没有进入纯远征模式");
            IsTrue(Game.Expedition.IsRunning, "没有排班也该用默认队伍打起来");
            AreEqual(GameDatabase.DefaultSquad.Length, Game.Expedition.Squad.Count, "默认队伍人数不对");

            Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            IsTrue(!Game.Expedition.IsRunning, "第一趟应该已经结束");
            IsTrue(Game.Manager.ExpeditionOnlyMode, "打完一趟不该自动退出纯远征模式");
            AreEqual((int)GameState.Expedition, (int)Game.Manager.State,
                     "纯远征模式打完一趟不该被送进闭店准备");

            // 不受「一天一趟」限制，立刻能再来一趟
            Game.Manager.StartAnotherLoopedExpedition();
            IsTrue(Game.Expedition.IsRunning, "打完一趟之后应该能立刻再来一趟");

            Game.Manager.ExitExpeditionOnlyMode();
            IsTrue(!Game.Manager.ExpeditionOnlyMode, "退出之后模式标记没清掉");
            IsTrue(!Game.Expedition.IsRunning, "退出纯远征模式应该把还在跑的远征收掉");
            AreEqual((int)GameState.Preparation, (int)Game.Manager.State, "退出后应该落回闭店准备");
        });

        static void Test_SquadCapIsEnforced()
        {
            StaffRoster.Reset();

            AreEqual(4, StaffRoster.All.Count, "名册人数（§1.5 原型规模：4 名怪物员工）");
            AreEqual(StaffRoster.MaxSquadSize, StaffRoster.SquadSize,
                     "默认排班应该正好把远征队排满");

            // 满员时再往里塞会被拒
            StaffRoster.Entry bench = null;
            for (int i = 0; i < StaffRoster.All.Count; i++)
                if (!StaffRoster.All[i].onExpedition) bench = StaffRoster.All[i];

            IsTrue(bench != null, "默认排班应该留一个人在店里");
            IsTrue(!StaffRoster.SetOnExpedition(bench.staffId, true),
                   $"远征队已经满员，{bench.Data.displayName} 还能挤进去");
            AreEqual(StaffRoster.MaxSquadSize, StaffRoster.SquadSize, "队伍人数被撑爆了");

            // 腾出位置就能进
            var member = StaffRoster.All[0];
            IsTrue(member.onExpedition, "前置条件：第一个人应该在队里");
            StaffRoster.SetOnExpedition(member.staffId, false);
            AreEqual(StaffRoster.MaxSquadSize - 1, StaffRoster.SquadSize, "换人没生效");

            IsTrue(StaffRoster.SetOnExpedition(bench.staffId, true),
                   "腾出位置之后还是进不去");
            AreEqual(StaffRoster.MaxSquadSize, StaffRoster.SquadSize, "换上来之后人数不对");

            // 夜班岗位和出征是两个轴，互不干扰
            StaffRoster.SetNightJob(bench.staffId, StaffAssignment.Security);
            IsTrue(bench.onExpedition && bench.nightJob == StaffAssignment.Security,
                   "同一个人不能同时排出征和夜班 —— §4.4 的连轴转就无从谈起了");
            IsTrue(bench.IsDoubleShift, "白天出征 + 晚上上岗应该判成连轴转");
        }

        /// <summary>
        /// §4.4「白天远征后继续值夜班会快速累积（疲劳）」。
        /// 连轴转的人要吃两份；只有整天什么都没干的人才回血 ——
        /// 否则「白天出征、晚上休息」就变成没有代价了。
        /// </summary>
        static void Test_DoubleShiftCostsMoreFatigue()
        {
            StaffRoster.Reset();

            var all = StaffRoster.All;
            var doubleShift = all[0];
            var expeditionOnly = all[1];
            var nightOnly = all[2];
            var resting = all[3];

            StaffRoster.SetOnExpedition(doubleShift.staffId, true);
            StaffRoster.SetNightJob(doubleShift.staffId, StaffAssignment.Cashier);

            StaffRoster.SetOnExpedition(expeditionOnly.staffId, true);
            StaffRoster.SetNightJob(expeditionOnly.staffId, StaffAssignment.Rest);

            StaffRoster.SetOnExpedition(nightOnly.staffId, false);
            StaffRoster.SetNightJob(nightOnly.staffId, StaffAssignment.Restock);

            StaffRoster.SetOnExpedition(resting.staffId, false);
            StaffRoster.SetNightJob(resting.staffId, StaffAssignment.Rest);

            for (int i = 0; i < all.Count; i++) all[i].fatigue = 0f;

            // 走一整天：远征回来结一次，营业结束再结一次
            StaffRoster.ApplyExpeditionFatigue();
            StaffRoster.ApplyNightShiftFatigue();

            AreEqualFloat(StaffRoster.ExpeditionFatigue + StaffRoster.NightShiftFatigue,
                          doubleShift.fatigue, "连轴转的人没有吃两份疲劳");
            AreEqualFloat(StaffRoster.ExpeditionFatigue, expeditionOnly.fatigue,
                          "只出征的人的疲劳");
            AreEqualFloat(StaffRoster.NightShiftFatigue, nightOnly.fatigue,
                          "只值夜班的人的疲劳");
            AreEqualFloat(0f, resting.fatigue, "整天休息的人不该有疲劳");

            IsTrue(doubleShift.fatigue > expeditionOnly.fatigue &&
                   doubleShift.fatigue > nightOnly.fatigue,
                   "连轴转必须比只干一头更累，否则排班没有代价");

            // 白天出征、晚上休息的人：那份远征疲劳要留着，不能被休息抹平
            IsTrue(expeditionOnly.fatigue > 0f,
                   "出征回来当晚休息就把远征疲劳抹平了 —— 出征等于没有代价");

            // 真正整天休息的人才回血
            resting.fatigue = StaffRoster.MaxFatigue;
            StaffRoster.ApplyNightShiftFatigue();
            AreEqualFloat(StaffRoster.MaxFatigue - StaffRoster.RestRecovery, resting.fatigue,
                          "整天休息没有回复疲劳");
        }

        static void Test_FatigueLowersEfficiencyWithFloor()
        {
            StaffRoster.Reset();

            var entry = StaffRoster.All[0];

            entry.fatigue = 0f;
            AreEqualFloat(1f, StaffRoster.Efficiency(entry), "没疲劳时效率应该是满的");

            entry.fatigue = StaffRoster.MaxFatigue;
            AreEqualFloat(StaffRoster.MinEfficiency, StaffRoster.Efficiency(entry),
                          "疲劳拉满时的效率");

            IsTrue(StaffRoster.MinEfficiency > 0f,
                   "效率不能归零，否则累坏的员工等于凭空消失，排班只剩「全体休息」一种解");

            // 中间要单调下降，不能是个只在两端生效的开关
            entry.fatigue = StaffRoster.MaxFatigue * 0.5f;
            float mid = StaffRoster.Efficiency(entry);
            IsTrue(mid < 1f && mid > StaffRoster.MinEfficiency,
                   $"半疲劳时的效率 {mid:0.00} 没有落在中间，疲劳成了开关而不是曲线");

            IsTrue(StaffRoster.IsExhausted(new StaffRoster.Entry
            {
                fatigue = StaffRoster.ExhaustedThreshold
            }), "到了阈值却没判成累坏了");

            // 没人在岗 = 效率 0（这个岗位今晚没人管）
            for (int i = 0; i < StaffRoster.All.Count; i++)
                StaffRoster.SetNightJob(StaffRoster.All[i].staffId, StaffAssignment.Rest);

            AreEqualFloat(0f, StaffRoster.EfficiencyOn(StaffAssignment.Cashier),
                          "没排收银岗，效率却不是 0");
        }

        /// <summary>§4.3「收银：决定结账速度、错误率和排队耐心」。</summary>
        static void Test_CashierOnDutyEasesCheckout()
        {
            StaffRoster.Reset();

            var checkout = Game.Store.Checkout;
            checkout.SetLevel(0);

            // 先把所有人撤下夜班，拿到「没人收银」的基线
            for (int i = 0; i < StaffRoster.All.Count; i++)
                StaffRoster.SetNightJob(StaffRoster.All[i].staffId, StaffAssignment.Rest);

            float baseScan = checkout.ScanWindow;
            float baseQueue = checkout.QueuePatienceMultiplier;

            AreEqualFloat(GameConfig.ScanBaseWindow, baseScan,
                          "没人收银时扫描判定区应该是基线");
            AreEqualFloat(1f, baseQueue, "没人收银时排队掉耐心应该是基线");

            // 排一个精神饱满的收银员
            var cashier = StaffRoster.All[0];
            cashier.fatigue = 0f;
            StaffRoster.SetNightJob(cashier.staffId, StaffAssignment.Cashier);

            IsTrue(checkout.ScanWindow > baseScan,
                   "排了收银岗，扫描判定区却没变宽");
            IsTrue(checkout.QueuePatienceMultiplier < baseQueue,
                   "排了收银岗，排队掉耐心却没变慢");

            AreEqualFloat(GameConfig.ScanBaseWindow * (1f + GameConfig.CashierScanBonus),
                          checkout.ScanWindow, "满效率收银员的扫描判定区");

            // 累坏的收银员帮不上那么多忙（§4.4）
            float freshScan = checkout.ScanWindow;
            cashier.fatigue = StaffRoster.MaxFatigue;

            IsTrue(checkout.ScanWindow < freshScan,
                   "收银员累坏了，判定区却没缩回去");
            IsTrue(checkout.ScanWindow > baseScan,
                   "累坏的收银员还不如没人 —— 效率有下限，不该跌破基线");
        }

        /// <summary>§4.3「补货：从仓库搬运商品并维持货架库存」。</summary>
        static void Test_RestockerRefillsShelvesDuringBusiness()
        {
            StaffRoster.Reset();

            var jelly = GameDatabase.GetProduct("glow_jelly");
            var shelf = Game.Store.FindShelf(jelly);
            IsTrue(shelf != null, "找不到发光果冻的货架");

            // 货架空着、仓库有货 —— 这正是「该有人去补」的局面
            shelf.count = 0;
            shelf.Refresh();
            Game.Store.AddToWarehouse(jelly, 10);

            // 没排补货岗：搬多久都不动
            for (int i = 0; i < StaffRoster.All.Count; i++)
                StaffRoster.SetNightJob(StaffRoster.All[i].staffId, StaffAssignment.Rest);

            AreEqual(0, Game.Store.TickStaffRestock(GameConfig.StaffRestockSeconds * 5f),
                     "没排补货岗，货架却自己满了");
            AreEqual(0, shelf.count, "没排补货岗，货架却自己满了");

            // 排上补货岗
            var restocker = StaffRoster.All[0];
            restocker.fatigue = 0f;
            StaffRoster.SetNightJob(restocker.staffId, StaffAssignment.Restock);

            // 不到间隔不该动
            AreEqual(0, Game.Store.TickStaffRestock(GameConfig.StaffRestockSeconds * 0.4f),
                     "还没到间隔就搬货了");

            int moved = Game.Store.TickStaffRestock(GameConfig.StaffRestockSeconds);
            IsTrue(moved > 0, "排了补货岗却没往货架上搬");
            AreEqual(moved, shelf.count, "搬过去的数量和货架上的对不上");
            AreEqual(10 - moved, Game.Store.WarehouseCount(jelly), "仓库没有相应扣减");

            // 仓库空了就搬不动了，不能凭空变货
            Game.Store.TakeFromWarehouse(jelly, 99);
            int before = shelf.count;
            Game.Store.TickStaffRestock(GameConfig.StaffRestockSeconds * 3f);
            AreEqual(before, shelf.count, "仓库空了还能往货架上搬");
        }

        /// <summary>§4.3「安保：处理偷窃、争吵和危险顾客」。</summary>
        static void Test_SecurityOnlyBlocksWhenStaffed()
        {
            StaffRoster.Reset();

            for (int i = 0; i < StaffRoster.All.Count; i++)
                StaffRoster.SetNightJob(StaffRoster.All[i].staffId, StaffAssignment.Rest);

            AreEqualFloat(0f, RandomEventManager.SecurityBlockChance,
                          "没排安保岗，却有概率拦下事故");

            var guard = StaffRoster.All[0];
            guard.fatigue = 0f;
            StaffRoster.SetNightJob(guard.staffId, StaffAssignment.Security);

            AreEqualFloat(GameConfig.SecurityBlockChance, RandomEventManager.SecurityBlockChance,
                          "满效率安保的拦截概率");
            IsTrue(RandomEventManager.SecurityBlockChance < 1f,
                   "安保不能百分百拦下 —— 那样第二天的货架目标就没有风险了");

            // 累坏的安保拦得少（§4.4）
            float fresh = RandomEventManager.SecurityBlockChance;
            guard.fatigue = StaffRoster.MaxFatigue;

            IsTrue(RandomEventManager.SecurityBlockChance < fresh,
                   "安保累坏了，拦截概率却没下降");
            IsTrue(RandomEventManager.SecurityBlockChance > 0f,
                   "累坏的安保应该还剩一点用，不该归零");
        }

        /// <summary>
        /// 打怪升级 —— 经验攒够一级的量就升级，等级带来的伤害/生命加成必须看得见，
        /// 否则「打怪升级」就是个不影响数值的装饰性数字。
        /// </summary>
        static void Test_StaffLevelsUpFromXpAndBuffsCombat()
        {
            string id = StaffRoster.All[0].staffId;
            var entry = StaffRoster.Get(id);
            AreEqual(1, entry.level, "前置条件：初始应该是 1 级");

            float need = StaffRoster.XpToNext(1);
            IsTrue(!StaffRoster.AddXp(id, need - 1f), "经验还没攒够就升级了");
            AreEqual(1, entry.level, "经验没攒够，等级却变了");

            IsTrue(StaffRoster.AddXp(id, 1f), "经验攒够了却没有升级");
            AreEqual(2, entry.level, "升级后的等级不对");

            IsTrue(StaffRoster.DamageMultiplier(entry) > 1f, "升级后伤害倍率该大于 1，否则升级不影响战斗");
            IsTrue(StaffRoster.HealthMultiplier(entry) > 1f, "升级后生命倍率该大于 1");

            // 封顶：不管加多少经验都不能超过等级上限，经验条也不该无限累积
            StaffRoster.AddXp(id, 99999f);
            AreEqual(StaffRoster.MaxLevel, entry.level, "满级之后还能继续升级");
            AreEqualFloat(0f, entry.xp, "满级之后经验条该清零，不然界面会显示一条填不满/溢出的条");
            IsTrue(!StaffRoster.AddXp(id, 10f), "满级之后加经验还返回「升级了」");
        }

        /// <summary>用户明确要求「总人物也可以升级，升级后可以扩大背包容量」。</summary>
        static void Test_CaptainLevelsUpAndExpandsBagCapacity()
        {
            AreEqual(1, CaptainProgress.Level, "前置条件：初始应该是 1 级");
            AreEqual(ExpeditionManager.BaseBagCapacity, ExpeditionManager.BagCapacity,
                     "1 级时背包容量应该等于基础值");

            float need = CaptainProgress.XpToNext(1);
            IsTrue(!CaptainProgress.AddXp(need - 1f), "经验还没攒够就升级了");
            AreEqual(1, CaptainProgress.Level, "经验没攒够，等级却变了");

            IsTrue(CaptainProgress.AddXp(1f), "经验攒够了却没有升级");
            AreEqual(2, CaptainProgress.Level, "升级后的等级不对");
            AreEqual(ExpeditionManager.BaseBagCapacity + CaptainProgress.CapacityPerLevel,
                     ExpeditionManager.BagCapacity, "升级后背包容量没有变大");

            CaptainProgress.AddXp(99999f);
            AreEqual(CaptainProgress.MaxLevel, CaptainProgress.Level, "满级之后还能继续升级");
            AreEqualFloat(0f, CaptainProgress.Xp, "满级之后经验条该清零");
        }

        /// <summary>队长等级/经验是后加的字段，旧存档没有就该退回 1 级 0 经验。</summary>
        static void Test_CaptainLevelSurvivesSaveButNotNewRun() => WithIsolatedSaveFile(() =>
        {
            CaptainProgress.AddXp(CaptainProgress.XpToNext(1) + 5f);   // 升到 2 级，还剩一点经验
            int levelBefore = CaptainProgress.Level;
            float xpBefore = CaptainProgress.Xp;
            AreEqual(2, levelBefore, "前置条件：应该已经升到 2 级");

            Game.Manager.ResetRunState(1, 0);
            SaveSystem.Save();
            var save = SaveSystem.Load();

            CaptainProgress.Reset();
            SaveSystem.Apply(save, true);
            AreEqual(levelBefore, CaptainProgress.Level, "续玩后没有恢复队长等级");
            AreEqualFloat(xpBefore, CaptainProgress.Xp, "续玩后没有恢复队长经验");

            SaveSystem.Apply(save, false);
            AreEqual(1, CaptainProgress.Level, "开新局还带着上一局的队长等级");
            AreEqualFloat(0f, CaptainProgress.Xp, "开新局还带着上一局的队长经验");

            // 旧存档没有这两个字段 → JsonUtility 默认值 0/0 → 应该归一化成 1 级 0 经验
            var legacy = new SaveData { currentDay = 1 };
            SaveSystem.Apply(legacy, true);
            AreEqual(1, CaptainProgress.Level, "旧存档应该退回 1 级，而不是 0 级");
            AreEqualFloat(0f, CaptainProgress.Xp, "旧存档不该凭空带出经验");
        });

        /// <summary>
        /// 击杀经验按「今天出征的这几个人」均分 —— 不出征的人不该凭空跟着长经验，
        /// 自动普攻为主，把经验系在「谁抢到尾刀」上并不公平。
        /// 队长也算一份存活战斗单位，走的是 CaptainProgress 那条独立的线。
        /// </summary>
        static void Test_ExpeditionKillsAwardXpToSquad() => WithIsolatedSaveFile(() =>
        {
            string[] squadIds = { "vampire_vera", "slime_bobo", "ghost_mia" };
            Game.Expedition.Begin(squadIds);
            try
            {
                AdvanceToRoom(RoomKind.Battle);

                var enemy = FirstAliveEnemy();
                IsTrue(enemy != null, "毒雾区应该有存活的刺藤精");
                float reward = enemy.Data.xpReward;
                IsTrue(reward > 0f, "前置条件：这只敌人得有经验值奖励，否则下面的断言测不出区别");

                var before = new float[squadIds.Length];
                for (int i = 0; i < squadIds.Length; i++)
                    before[i] = StaffRoster.Get(squadIds[i]).xp;
                float captainXpBefore = CaptainProgress.Xp;

                IsTrue(Game.Expedition.Captain.IsAlive, "前置条件：队长应该还活着，否则分母算不出来");
                enemy.Health.Damage(enemy.Data.maxHealth + 1f);

                // 分母 = 存活员工 + 队长
                float share = reward / (squadIds.Length + 1);
                for (int i = 0; i < squadIds.Length; i++)
                {
                    var entry = StaffRoster.Get(squadIds[i]);
                    AreEqualFloat(before[i] + share, entry.xp,
                        $"{squadIds[i]} 分到的经验不对（应该按存活战斗单位 {squadIds.Length + 1} 份均分）");
                }
                AreEqualFloat(captainXpBefore + share, CaptainProgress.Xp, "队长分到的经验不对");

                // 没出征的人不该凭空长经验
                var bench = StaffRoster.All;
                for (int i = 0; i < bench.Count; i++)
                {
                    if (System.Array.IndexOf(squadIds, bench[i].staffId) >= 0) continue;
                    AreEqualFloat(0f, bench[i].xp, $"没出征的 {bench[i].staffId} 却涨了经验");
                }
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>击败敌人直接掉金币，独立于商品掉落 —— 战斗要有实打实的正反馈。</summary>
        static void Test_ExpeditionKillsDropCoins() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Resource);

                var enemy = FirstAliveEnemy();
                IsTrue(enemy != null, "资源房应该有一只普通敌人");
                IsTrue(enemy.Data.coinMax > 0, "前置条件：这只敌人得配了金币奖励，否则测不出区别");

                int before = Game.Economy.Money;
                int revenueBefore = Game.Economy.DaySalesRevenue;

                enemy.Health.Damage(enemy.Data.maxHealth + 1f);

                int gained = Game.Economy.Money - before;
                IsTrue(gained >= enemy.Data.coinMin && gained <= enemy.Data.coinMax,
                       $"金币掉落数量不对：拿到 {gained}，应该在 {enemy.Data.coinMin}~{enemy.Data.coinMax} 之间");
                AreEqual(revenueBefore, Game.Economy.DaySalesRevenue,
                         "远征金币不该算进「今日销售额」，那栏是给营业表现看的");
            }
            finally
            {
                if (Game.Expedition.IsRunning) Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        static void Test_RosterSurvivesSaveButNotNewRun() => WithIsolatedSaveFile(() =>
        {
            StaffRoster.Reset();

            var all = StaffRoster.All;
            string doubleShiftId = all[0].staffId;

            // 排一个「白天出征 + 晚上安保」的连轴转，外加一点疲劳
            StaffRoster.SetOnExpedition(doubleShiftId, true);
            StaffRoster.SetNightJob(doubleShiftId, StaffAssignment.Security);
            StaffRoster.AddFatigue(doubleShiftId, 56f);

            Game.Manager.ResetRunState(2, 40);
            SaveSystem.Save();

            var save = SaveSystem.Load();
            IsTrue(save != null, "存档没读回来");
            IsTrue(save.staffRoster != null && save.staffRoster.Count == all.Count,
                   "存档里没有完整的名册");

            // 续玩 → 恢复
            StaffRoster.Reset();
            SaveSystem.Apply(save, true);

            var restored = StaffRoster.Get(doubleShiftId);
            IsTrue(restored.onExpedition, "续玩后没有恢复「今天出征」");
            AreEqual((int)StaffAssignment.Security, (int)restored.nightJob, "续玩后的夜班岗位");
            AreEqualFloat(56f, restored.fatigue, "续玩后的疲劳");

            // 开新局 → 跟着金钱一起丢掉，回到默认排班
            SaveSystem.Apply(save, false);

            var fresh = StaffRoster.Get(doubleShiftId);
            AreEqualFloat(0f, fresh.fatigue, "开新局还带着上一局的疲劳");
            AreEqual(StaffRoster.MaxSquadSize, StaffRoster.SquadSize,
                     "开新局没有回到默认排班");

            // 旧存档没有这个字段 → 空列表 → 默认排班，和加字段之前一致
            var legacy = new SaveData { currentDay = 2, staffRoster = null };
            SaveSystem.Apply(legacy, true);

            AreEqual(StaffRoster.MaxSquadSize, StaffRoster.SquadSize,
                     "旧存档应该退回默认排班，而不是抛异常");
            AreEqualFloat(0f, StaffRoster.All[0].fatigue, "旧存档不该凭空带出疲劳");
        });

        /// <summary>打怪升级的等级/经验是后加的两段字段，旧存档没有就该退回 1 级 0 经验。</summary>
        static void Test_StaffLevelSurvivesSaveButNotNewRun() => WithIsolatedSaveFile(() =>
        {
            StaffRoster.Reset();

            string id = StaffRoster.All[0].staffId;
            StaffRoster.AddXp(id, StaffRoster.XpToNext(1) + 5f);   // 升到 2 级，还剩一点经验
            var before = StaffRoster.Get(id);
            AreEqual(2, before.level, "前置条件：应该已经升到 2 级");
            IsTrue(before.xp > 0f, "前置条件：应该还剩一点没花完的经验");

            Game.Manager.ResetRunState(1, 0);
            SaveSystem.Save();
            var save = SaveSystem.Load();

            // 续玩 → 等级和经验都要原样恢复
            StaffRoster.Reset();
            SaveSystem.Apply(save, true);

            var restored = StaffRoster.Get(id);
            AreEqual(before.level, restored.level, "续玩后没有恢复等级");
            AreEqualFloat(before.xp, restored.xp, "续玩后没有恢复经验");

            // 开新局 → 跟着排班一起清空
            SaveSystem.Apply(save, false);
            AreEqual(1, StaffRoster.Get(id).level, "开新局还带着上一局的等级");
            AreEqualFloat(0f, StaffRoster.Get(id).xp, "开新局还带着上一局的经验");

            // 旧存档（只有 4 段、没有等级/经验）照样能读，退回 1 级 0 经验
            var legacy = new SaveData
            {
                currentDay = 1,
                staffRoster = new List<string> { $"{id}|1|0|0" }
            };
            SaveSystem.Apply(legacy, true);
            AreEqual(1, StaffRoster.Get(id).level, "旧存档应该退回 1 级，而不是抛异常");
            AreEqualFloat(0f, StaffRoster.Get(id).xp, "旧存档不该凭空带出经验");
        });

        /// <summary>
        /// 一趟远征要跑五六分钟。跑完之后退出游戏，重进必须落回<b>闭店准备</b>：
        ///   · 退回晨会 = 白跑一趟（战利品还在仓库里，但那一天要重来）；
        ///   · 还能再点一次出发 = 带着到手的货再刷一趟。
        /// 两种都不行。
        /// </summary>
        static void Test_FinishedExpeditionSurvivesReload() => WithIsolatedSaveFile(() =>
        {
            Game.Manager.ResetRunState(1, 0);
            Game.Manager.BeginNewDay();

            IsTrue(Game.Manager.StartDayExpedition(), "没能出发");
            Game.Expedition.Finish(ExpeditionOutcome.Retreated);

            // ReturnFromExpedition 自己存了一份
            var save = SaveSystem.Load();
            IsTrue(save != null, "远征回来没有存档 —— 一次退出就把这趟冲掉了");
            IsTrue(save.expeditionDoneToday, "存档没记下今天那趟远征已经用掉");
            IsTrue(!save.daySettled, "前置条件：这一天还没结算");
            IsTrue(SaveSystem.ShouldResumeAfterExpedition(save),
                   "读档决策没把它判成「远征已经跑完」");

            // 模拟重启：新进程里 GameManager 是干净的
            Game.Manager.ResetRunState(1, 0);
            Game.Manager.BeginNewDay();
            IsTrue(!Game.Manager.ExpeditionDoneToday, "前置条件：重启后应该是干净的");

            // GameBootstrap.BootGame() 的那一步
            if (SaveSystem.ShouldResumeAfterExpedition(save))
                Game.Manager.ResumeAfterExpedition();

            AreEqual((int)GameState.Preparation, (int)Game.Manager.State,
                     "重进应该落回闭店准备，而不是退回晨会");
            IsTrue(Game.Manager.ExpeditionDoneToday, "重进后今天的远征次数又满了");
            IsTrue(!Game.Manager.StartDayExpedition(), "重进后又刷了一趟远征");

            // 反向保证：结算过的存档接着玩的是「下一天」，那天的远征还没用过。
            //
            // 这一段必须先真的跑一趟远征再结算 —— 不然存档里的 expeditionDoneToday
            // 本来就是 false，下面那条断言两边都成立，等于没写。
            Game.Manager.ResetRunState(1, 0);
            Game.Manager.BeginNewDay();
            IsTrue(Game.Manager.StartDayExpedition(), "第二段没能出发");
            Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            Game.Manager.ConcludeDay();

            var settled = SaveSystem.Load();
            IsTrue(settled != null && settled.daySettled, "前置条件：这应该是一份结算存档");
            IsTrue(settled.expeditionDoneToday,
                   "前置条件不成立：这份存档必须同时带着「今天跑过远征」和「已结算」，" +
                   "否则下面那条断言恒真");
            IsTrue(!SaveSystem.ShouldResumeAfterExpedition(settled),
                   "结算存档接着打的是下一天，那天的远征不该被判成已经用掉");
        });

        /// <summary>
        /// 角色外观 —— 远征里的队长、员工、敌人以前全是纯色圆点（BuildBody 直接画一个
        /// SpriteFactory.Circle），现在要走 SpriteFactory 的角色外形系统。
        /// 这条用例不追求像素级校验，只保证每个外形都真的生成了贴图，
        /// 而且敌人都配了专属外形编号 —— silhouette 忘记赋值会悄悄退回默认值 0，
        /// 那样看着跟别的敌人没区别，问题要等真的进游戏才会被发现。
        /// </summary>
        static void Test_CharacterArtCoversStaffAndEnemies()
        {
            var staff = GameDatabase.Staff;
            IsTrue(staff.Count > 0, "前置条件：员工表不能是空的");

            for (int i = 0; i < staff.Count; i++)
            {
                var sprite = MonsterMart.Art.SpriteFactory.Character(staff[i]);
                IsTrue(sprite != null, $"{staff[i].displayName} 没能生成角色贴图");
                AreEqual(32, (int)sprite.rect.width, $"{staff[i].displayName} 贴图宽度不对");
                AreEqual(48, (int)sprite.rect.height, $"{staff[i].displayName} 贴图高度不对");
            }

            var enemies = GameDatabase.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                IsTrue(e.silhouette >= 6,
                       $"{e.displayName} 没有配专属外形（silhouette={e.silhouette}），会退回和别的敌人一样的默认人形");

                var sprite = MonsterMart.Art.SpriteFactory.Character(e);
                IsTrue(sprite != null, $"{e.displayName} 没能生成角色贴图");
                AreEqual(32, (int)sprite.rect.width, $"{e.displayName} 贴图宽度不对");
                AreEqual(48, (int)sprite.rect.height, $"{e.displayName} 贴图高度不对");
            }
        }

        /// <summary>
        /// UI 层以前一行覆盖都没有 —— 而它恰恰是「一跑起来就炸」风险最高的地方：
        /// 面板全是代码搭的，少接一个字段、Refresh 里读一个空引用，
        /// 都要等真的按下 Play 才会发现。
        ///
        /// 这条用例把整套 Canvas 搭出来、逐个打开面板并跑一遍刷新，
        /// 专门拦空引用和搭不起来的版式。
        /// </summary>
        static void Test_AllPanelsBuildAndOpen()
        {
            var uiGo = new GameObject("UIRootSandbox");
            uiGo.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var ui = uiGo.AddComponent<UIRoot>();
                ui.Build();
                Game.UI = ui;

                IsTrue(ui.Canvas != null, "Canvas 没搭出来");
                IsTrue(ui.MorningBrief != null, "晨会面板没建出来");

                Game.Day.SetDay(1);
                Game.Day.PrepareDay();

                // 晨会：排班行要按名册长出来，切换分工后刷新不能炸
                ui.ShowMorningBrief();
                IsTrue(ui.MorningBrief.IsOpen, "晨会面板打不开");

                var first = StaffRoster.All[0];
                StaffRoster.ToggleExpedition(first.staffId);
                StaffRoster.CycleNightJob(first.staffId);
                ui.ShowMorningBrief();          // 再开一次 = 再刷新一次

                ui.CloseMorningBrief();
                IsTrue(!ui.MorningBrief.IsOpen, "晨会面板关不掉");

                // 闭店准备：进货列表 + 货架预览 + 远征状态行
                ui.ShowPreparation();
                IsTrue(ui.Preparation.IsOpen, "备货面板打不开");
                ui.ClosePreparation();

                // 结算与结局
                ui.ShowSettlement(Game.Day.BuildSummary());
                IsTrue(ui.Settlement.IsOpen, "结算面板打不开");
                ui.Settlement.Close();

                ui.ShowEnding(EndingType.Normal, "测试用结局文案");
                IsTrue(ui.Ending.IsOpen, "结局面板打不开");
                ui.Ending.Close();

                // 远征抬头：它在远征里每帧刷新，最容易读到空的队伍
                ui.ShowExpedition();
                IsTrue(ui.Expedition.IsOpen, "远征面板打不开");
                ui.CloseExpedition();

                // 队员信息面板：远征还没开始（队伍是空的、队长是 null）时也不能崩
                ui.ToggleSquadInfo();
                IsTrue(ui.SquadInfo.IsOpen, "队员信息面板打不开");
                ui.ToggleSquadInfo();
                IsTrue(!ui.SquadInfo.IsOpen, "队员信息面板关不掉");

                ui.ToggleBestiary();
                ui.ToggleBestiary();

                // 暂停菜单：还没进纯远征模式时按钮文案应该是入口文案
                // （模式切换本身连带的存档副作用，交给下面 WithIsolatedSaveFile
                // 包着的 Test_ExpeditionOnlyModeLoopsWithoutDayCycle 去测，这里
                // 只确认面板搭得起来、默认文案不对）
                ui.ShowPauseMenu();
                IsTrue(ui.Pause.IsOpen, "暂停菜单打不开");
                IsTrue(ui.Pause.ExpeditionModeLabelText.Contains("纯远征"),
                       "还没进纯远征模式时按钮文案不对");
                ui.ClosePauseMenu();

                ui.ShowChoice("测试", "测试正文",
                              new ChoiceOption("确定", "", () => { }));
                IsTrue(ui.Choice.IsOpen, "选择弹窗打不开");
                ui.Choice.Close();

                ui.CloseAllPanels();
                IsTrue(!ui.BlocksWorldInput, "关光了所有面板，世界输入却还被挡着");
            }
            finally
            {
                Game.UI = null;
                UnityEngine.Object.DestroyImmediate(uiGo);
            }
        }

        /// <summary>
        /// 用户反馈明确要求「即便开始营业了也可以打开商店进行补货，随时可以补货」。
        /// 以前 B 键只在闭店准备阶段生效，现在营业中也要能随时调出进货界面，
        /// 并且「开始营业」按钮得让位 —— 店已经开着了，再点一次会把计时器冲掉。
        /// </summary>
        static void Test_PreparationViewOpensDuringBusiness()
        {
            var uiGo = new GameObject("UIRootSandbox_Preparation");
            uiGo.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var ui = uiGo.AddComponent<UIRoot>();
                ui.Build();
                Game.UI = ui;

                EnterDay(1);

                // 闭店准备阶段：正常样子，「开始营业」按钮该在
                ui.ShowPreparation();
                IsTrue(!ui.Preparation.DuringBusiness, "闭店准备阶段不该被判成「营业中」");
                IsTrue(ui.Preparation.StartButtonVisible, "闭店准备阶段「开始营业」按钮不该被藏起来");
                ui.ClosePreparation();

                // 营业开始：进货界面要能随时再打开，这次「开始营业」按钮该让位
                Game.Manager.OpenStore();
                float timeBefore = Game.Day.TimeRemaining;

                ui.ShowPreparation();
                IsTrue(ui.Preparation.IsOpen, "营业中进货界面打不开");
                IsTrue(ui.Preparation.DuringBusiness, "营业中打开却没被判成「营业中」");
                IsTrue(!ui.Preparation.StartButtonVisible, "营业中「开始营业」按钮该被藏起来");

                // 双重保险：就算误触发 TryBeginBusiness，也不能把营业状态和计时器冲掉，
                // 更不能弹出「仓库是空的」这种只该在闭店准备阶段出现的警告
                // （前置条件：沙盒刚初始化，仓库确实是空的，否则这条测不出保护有没有生效）
                int warehouseTotal = 0;
                for (int i = 0; i < GameDatabase.Products.Count; i++)
                    warehouseTotal += Game.Store.WarehouseCount(GameDatabase.Products[i]);
                IsTrue(warehouseTotal == 0, "前置条件：仓库应该是空的，否则下面这条测不出保护有没有生效");

                ui.Preparation.TryBeginBusiness();
                AreEqual((int)GameState.Open, (int)Game.Manager.State, "营业中误触发开始营业，状态被改了");
                AreEqualFloat(timeBefore, Game.Day.TimeRemaining, "营业中误触发开始营业，计时器被重置了");
                IsTrue(!ui.Choice.IsOpen,
                       "营业中误触发开始营业，弹出了「仓库是空的」警告 —— 保护没生效");

                ui.ClosePreparation();
                IsTrue(!ui.Preparation.IsOpen, "营业中进货界面关不掉");
            }
            finally
            {
                Time.timeScale = 1f;
                Game.UI = null;
                UnityEngine.Object.DestroyImmediate(uiGo);
            }
        }

        /// <summary>
        /// 用户反馈明确要求「直接点击一件结账，不需要一个个拖到扫描区域」——
        /// 扫描现在是点一下就成功，不需要任何拖拽/位置判定。收银台升级 / 收银岗位
        /// 的加成挪到了「两次扫描之间的间隔」上，得验证这个间隔真的在拦人，
        /// 而不是形同虚设。
        /// </summary>
        static void Test_CheckoutScanIsClickBasedNotPositional()
        {
            var uiGo = new GameObject("UIRootSandbox_Checkout");
            uiGo.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var ui = uiGo.AddComponent<UIRoot>();
                ui.Build();
                Game.UI = ui;

                var jelly = GameDatabase.GetProduct("glow_jelly");
                IsTrue(jelly != null, "前置条件：找不到测试用商品 glow_jelly");

                var customer = SpawnCustomer(MonsterType.Slime);
                customer.Basket.Add(jelly);
                customer.Basket.Add(jelly);

                var checkout = Game.Store.Checkout;
                ui.ShowCheckout(checkout, customer);

                IsTrue(ui.CheckoutPanel.IsOpen, "收银界面打不开");
                AreEqual(2, ui.CheckoutPanel.ItemCount, "台面上的商品数量不对");
                IsTrue(!ui.CheckoutPanel.IsItemScanned(0), "前置条件：还没点就已经算扫描过了");

                // 点一下就扫，不需要任何拖拽/位置判定
                ui.CheckoutPanel.ClickItem(0);
                IsTrue(ui.CheckoutPanel.IsItemScanned(0), "点一下商品应该直接扫描成功");

                // 扫描间隔还没过：紧接着点下一件应该被挡住，否则收银台升级就没有意义了
                ui.CheckoutPanel.ClickItem(1);
                IsTrue(!ui.CheckoutPanel.IsItemScanned(1), "扫描间隔还没到就能扫下一件");

                // 间隔过去之后应该能正常扫
                ui.CheckoutPanel.TickScanLock(999f);
                ui.CheckoutPanel.ClickItem(1);
                IsTrue(ui.CheckoutPanel.IsItemScanned(1), "间隔过了却还是扫不了");

                // 重复点已经扫过的商品要有处罚（间隔过去之后才轮到这条判定）
                ui.CheckoutPanel.TickScanLock(999f);
                int repBefore = Game.Reputation.Value;
                ui.CheckoutPanel.ClickItem(0);
                AreEqual(repBefore + GameConfig.RepScanError, Game.Reputation.Value, "重复扫描没有扣声望");

                ui.CloseCheckout();
                IsTrue(!ui.CheckoutPanel.IsOpen, "收银界面关不掉");
            }
            finally
            {
                Game.UI = null;
                UnityEngine.Object.DestroyImmediate(uiGo);
            }
        }

        /// <summary>
        /// 用户反馈「靠近了结算台按 E 没反应，没办法给客人结算」——队首顾客还在
        /// 走向排队点的那一瞬间，以前 IsAvailable 直接判 false，玩家按 E 像按了空气。
        /// 现在这种情况下也该算「可交互」，只是提示告诉玩家等一下。
        /// </summary>
        static void Test_CheckoutGivesFeedbackWhenCustomerNotYetAtCounter() => WithIsolatedSaveFile(() =>
        {
            EnterDay(2);
            Game.Manager.OpenStore();

            var checkout = Game.Store.Checkout;
            var customer = SpawnCustomer(MonsterType.Vampire);
            checkout.Enqueue(customer);

            // 沙盒里没搭 PlayerController（BuildSandbox 只建了 Managers/Store/Spawner），
            // 但 Checkout.IsAvailable/GetPrompt/OnInteract 都不读这个参数，传 null 一样能测
            IsTrue(!checkout.HeadReady, "前置条件：顾客不该已经站定");
            IsTrue(checkout.IsAvailable(null),
                   "队里有人但还没走到，收银台应该还算「可交互」，只是给个提示，而不是彻底不可用");
            IsTrue(!string.IsNullOrEmpty(checkout.GetPrompt(null)),
                   "顾客还没到位时也该有提示文案，不能是空气");

            checkout.OnInteract(null);
            IsTrue(!checkout.SessionOpen, "顾客还没到位，收银会话却被打开了");
        });

        /// <summary>
        /// 用户反馈明确描述为「卡bug了」——货架满了、玩家手上正好拿着这个商品时，
        /// 以前 IsAvailable 直接判 false，按 E 像按了空气，人也没法腾出手接下一件。
        /// 现在这种情况也该算「可交互」，只是给个「满了」的提示。
        /// </summary>
        static void Test_FullShelfGivesFeedbackInsteadOfSilence()
        {
            var jelly = GameDatabase.GetProduct("glow_jelly");
            var shelf = Game.Store.FindShelf(jelly);
            IsTrue(shelf != null, "前置条件：找不到发光果冻的货架");

            shelf.count = shelf.capacity;   // 货架装满
            shelf.Refresh();

            var player = SpawnPlayer();
            Game.Store.AddToWarehouse(jelly, 3);
            player.TakeFromWarehouse(jelly);
            AreEqual(3, player.Carry.Count, "前置条件：玩家手上应该有 3 件");

            IsTrue(shelf.IsAvailable(player), "货架满了却判成不可交互——按 E 会像按了空气");
            IsTrue(shelf.GetPrompt(player).Contains("满"), "满货架的提示文案不对");

            int before = shelf.count;
            shelf.OnInteract(player);
            AreEqual(before, shelf.count, "满货架不该继续往上叠货");
            AreEqual(3, player.Carry.Count, "满货架不该把玩家手上的货凭空吃掉");
        }

        /// <summary>
        /// 用户明确要求「应该能把手上的东西放回仓库，而不是只能切换商品」——
        /// 以前唯一「腾空手」的办法是去仓库点一个有库存的其他商品，顺带把手上的
        /// 换掉；货架满了没处卸、又不想拿件不需要的东西时会被卡住。
        /// </summary>
        static void Test_StockRoomCanPutBackCarriedItem()
        {
            var uiGo = new GameObject("UIRootSandbox_StockRoom");
            uiGo.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var ui = uiGo.AddComponent<UIRoot>();
                ui.Build();
                Game.UI = ui;

                var player = SpawnPlayer();
                var jelly = GameDatabase.GetProduct("glow_jelly");
                Game.Store.AddToWarehouse(jelly, 5);
                player.TakeFromWarehouse(jelly);
                AreEqual(5, player.Carry.Count, "前置条件：手上应该有货");

                int warehouseBefore = Game.Store.WarehouseCount(jelly);

                ui.ShowStockRoom();
                IsTrue(ui.StockRoomPicker.IsOpen, "仓库界面打不开");

                ui.StockRoomPicker.PutBackCarry();
                IsTrue(player.Carry.IsEmpty, "点了放回仓库，手上还有东西");
                AreEqual(warehouseBefore + 5, Game.Store.WarehouseCount(jelly), "放回仓库的数量不对");
            }
            finally
            {
                Game.UI = null;
                UnityEngine.Object.DestroyImmediate(uiGo);
            }
        }

        /// <summary>
        /// 用户反馈「有时候人走到收银机旁按 E 也结不了账」——顾客卡在半路走不到
        /// 排队点（寻路偶尔失败之类）时，以前会永远卡着，HeadReady 永远是
        /// false。卡够久应该直接吸附到排队点，不能让玩家干等。
        /// </summary>
        static void Test_StuckQueueCustomerSnapsToSlot()
        {
            var vampire = SpawnCustomer(MonsterType.Vampire);
            Game.Store.Checkout.Enqueue(vampire);

            IsTrue(!vampire.IsAtQueueSlot, "前置条件：刚入队不该已经站定");

            // 模拟顾客卡在半路走不到（现实里对应寻路偶尔失败）——只推进
            // TickWaitingInQueue，不真的移动
            for (int i = 0; i < 10; i++)
                vampire.TickWaitingInQueue(0.3f);   // 10 × 0.3 = 3s，超过 QueueStuckSeconds(2s)

            IsTrue(vampire.IsAtQueueSlot, "卡够久之后应该自动吸附到排队点，不能让顾客卡死");
        }

        static EnemyController FirstEnemyOfTier(EnemyTier tier)
        {
            var enemies = Game.Expedition.Enemies;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && enemies[i].IsAlive && enemies[i].Tier == tier)
                    return enemies[i];
            return null;
        }

        static int CountAliveOfTier(EnemyTier tier)
        {
            int n = 0;
            var enemies = Game.Expedition.Enemies;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && enemies[i].IsAlive && enemies[i].Tier == tier) n++;
            return n;
        }

        /// <summary>队长挨个走到喷口上按 E —— 就是玩家实际要做的那一圈。</summary>
        static void CloseAllVentsByWalking()
        {
            var vents = Game.Expedition.Vents;
            for (int i = 0; i < vents.Count; i++)
            {
                if (!vents[i].IsOpen) continue;

                Game.Expedition.Captain.TeleportTo(StoreGrid.WorldToCell(vents[i].Position));
                IsTrue(Game.Expedition.CloseVentInReach(),
                       $"站在第 {i} 个喷口上却关不掉它");
            }
        }

        /// <summary>沿路线推进到指定类型的房间，路上遇到的敌人一律清掉。</summary>
        static void AdvanceToRoom(RoomKind kind)
        {
            for (int guard = 0; guard < 16; guard++)
            {
                var room = Game.Expedition.CurrentRoom;
                if (room != null && room.kind == kind) return;

                IsTrue(Game.Expedition.IsRunning, $"还没走到 {kind} 房，远征就结束了");
                IsTrue(!Game.Expedition.IsLastRoom, $"走到最后一间也没找到 {kind} 房");

                KillAllEnemies();
                Game.Expedition.AdvanceRoom();
            }
            throw new Exception($"没能走到 {kind} 房间");
        }

        static void KillAllEnemies()
        {
            var enemies = Game.Expedition.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.Health.Damage(enemy.Data.maxHealth + 1f);
            }
        }

        static EnemyController FirstAliveEnemy()
        {
            var enemies = Game.Expedition.Enemies;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && enemies[i].IsAlive) return enemies[i];
            return null;
        }

        static List<EnemyController> AliveEnemies()
        {
            var result = new List<EnemyController>();
            var enemies = Game.Expedition.Enemies;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && enemies[i].IsAlive) result.Add(enemies[i]);
            return result;
        }

        static EnemyController NearestAliveEnemyFrom(StaffFollower from)
            => PickAliveEnemy(from, nearest: true);

        static EnemyController FarthestAliveEnemyFrom(StaffFollower from)
            => PickAliveEnemy(from, nearest: false);

        static EnemyController PickAliveEnemy(StaffFollower from, bool nearest)
        {
            EnemyController best = null;
            float bestDistance = nearest ? float.MaxValue : float.MinValue;

            var enemies = Game.Expedition.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;

                float d = from.DistanceTo(enemy);
                if (nearest ? d >= bestDistance : d <= bestDistance) continue;

                bestDistance = d;
                best = enemy;
            }
            return best;
        }

        /// <summary>
        /// §18 第二阶段的完成标准里明确写了「队友不会长期卡住」。
        /// 这里把队友硬塞到房间另一头，确认归队逻辑能把它拉回队长身边。
        /// </summary>
        static void Test_StuckFollowerReturnsToCaptain() => WithIsolatedSaveFile(() =>
        {
            Game.Expedition.Begin();
            try
            {
                AdvanceToRoom(RoomKind.Battle);

                var captain = Game.Expedition.Captain;
                var follower = Game.Expedition.Squad[0];
                var grid = Game.Expedition.World.Grid;

                // 队友出生就在队长脚下，直接断言「离队长很近」会永远成立。
                // 先把它扔到房间另一头（敌人刷新点离营地至少 6 格），再验证归队。
                var faraway = FirstAliveEnemy();
                IsTrue(faraway != null, "需要一个远处的敌人当落点");

                follower.Unstick(faraway);
                float before = follower.DistanceTo(captain);
                IsTrue(before > 3f,
                       $"前置条件不成立：队友没被挪远，只有 {before:0.00} 格");

                follower.Unstick(captain);

                float after = follower.DistanceTo(captain);
                IsTrue(after <= 1.5f,
                       $"归队后应该紧贴队长，实际相距 {after:0.00}（归队前 {before:0.00}）");
                IsTrue(grid.IsWalkable(follower.Cell), "归队后落在了墙里");
            }
            finally
            {
                Game.Expedition.Finish(ExpeditionOutcome.Retreated);
            }
        });

        /// <summary>
        /// 存档路径是 Application.persistentDataPath，和真机上玩的是同一个文件。
        /// 跑测试不能把用户手上的存档冲掉，所以进出各备份 / 还原一次。
        /// </summary>
        static void WithIsolatedSaveFile(Action body)
        {
            string path = SaveSystem.FilePath;
            bool had = File.Exists(path);
            string backup = had ? File.ReadAllText(path) : null;

            try
            {
                if (had) File.Delete(path);
                body();
            }
            finally
            {
                if (had) File.WriteAllText(path, backup);
                else if (File.Exists(path)) File.Delete(path);
            }
        }

        // ------------------------------------------------------------------
        // 沙箱：用代码装配一份最小运行时（和 GameBootstrap 同样的顺序，去掉 UI / 摄像机 / 音频）
        // ------------------------------------------------------------------
        static GameObject BuildSandbox()
        {
            Game.Clear();
            InteractableRegistry.Clear();
            CustomerRegistry.Clear();
            BestiaryTracker.Reset();
            ExpeditionProgress.Reset();
            StaffRoster.Reset();
            CaptainProgress.Reset();
            GameDatabase.Reset();
            GameDatabase.EnsureBuilt();

            var root = new GameObject("[SmokeTestSandbox]");
            root.hideFlags = HideFlags.HideAndDontSave;

            var managers = new GameObject("Managers");
            managers.transform.SetParent(root.transform, false);

            var economy = managers.AddComponent<EconomyManager>();
            economy.Initialize(GameConfig.StartingMoney);
            Game.Economy = economy;

            var reputation = managers.AddComponent<ReputationManager>();
            reputation.Initialize(50);       // 见 Test_InspectionIsNotAppliedTwice 的注释
            Game.Reputation = reputation;

            var cleanliness = managers.AddComponent<CleanlinessManager>();
            cleanliness.Initialize(GameConfig.StartingCleanliness);
            Game.Cleanliness = cleanliness;

            Game.Day = managers.AddComponent<DayManager>();
            Game.Events = managers.AddComponent<RandomEventManager>();
            Game.Manager = managers.AddComponent<GameManager>();

            var storeGo = new GameObject("Store");
            storeGo.transform.SetParent(root.transform, false);
            var store = storeGo.AddComponent<StoreWorld>();
            store.Build();
            Game.Store = store;

            var spawnerGo = new GameObject("Spawner");
            spawnerGo.transform.SetParent(root.transform, false);
            Game.Spawner = spawnerGo.AddComponent<CustomerSpawner>();

            var expeditionGo = new GameObject("Expedition");
            expeditionGo.transform.SetParent(root.transform, false);
            Game.Expedition = expeditionGo.AddComponent<ExpeditionManager>();

            return root;
        }

        static void TeardownSandbox(GameObject sandbox)
        {
            // 顾客是 Spawn 出来的，不一定挂在沙箱底下
            var all = new List<CustomerController>(CustomerRegistry.All);
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null) UnityEngine.Object.DestroyImmediate(all[i].gameObject);

            CustomerRegistry.Clear();
            InteractableRegistry.Clear();

            if (sandbox != null) UnityEngine.Object.DestroyImmediate(sandbox);

            Game.Clear();
            GameDatabase.Reset();
        }

        static void EnterDay(int day)
        {
            Game.Day.SetDay(day);
            Game.Day.PrepareDay();
            AreEqual(day, Game.Day.CurrentDay, "当前天数");
            IsTrue(Game.Day.CurrentPlan != null, $"第 {day} 天没有 DayPlan");
        }

        static CustomerController SpawnCustomer(MonsterType type)
        {
            var customer = Game.Spawner.Spawn(type);
            IsTrue(customer != null, $"{type} 没有生成出来");
            return customer;
        }

        /// <summary>在店里放一个还没结账的检查员。</summary>
        static CustomerController SpawnInspectorInStore()
        {
            var inspector = SpawnCustomer(MonsterType.Inspector);
            IsTrue(!inspector.Served, "前置条件不成立：检查员不该已经结过账");
            return inspector;
        }

        /// <summary>
        /// BuildSandbox 不建 PlayerController（默认沙盒不需要）——需要真的测
        /// PlayerCarry 相关交互时（货架/仓库）现建一个，挂在 Store 同一个父节点下，
        /// 这样 TeardownSandbox 销毁沙盒根节点时会一并清掉。
        /// </summary>
        static PlayerController SpawnPlayer()
        {
            var go = new GameObject("TestPlayer");
            if (Game.Store != null) go.transform.SetParent(Game.Store.transform.parent, false);

            var player = go.AddComponent<PlayerController>();
            player.Initialize(Game.Store.PlayerStartCell);
            Game.Player = player;
            return player;
        }

        // ------------------------------------------------------------------
        // 断言
        // ------------------------------------------------------------------
        static void IsTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        static void AreEqual(int expected, int actual, string what)
        {
            if (expected != actual)
                throw new Exception($"{what}：期望 {expected}，实际 {actual}");
        }

        static void AreEqualFloat(float expected, float actual, string what)
        {
            if (!Mathf.Approximately(expected, actual))
                throw new Exception($"{what}：期望 {expected}，实际 {actual}");
        }

        static void AreEqual(string expected, string actual, string what)
        {
            if (expected != actual)
                throw new Exception($"{what}：期望 {expected}，实际 {actual}");
        }
    }
}
