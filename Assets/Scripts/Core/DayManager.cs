using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Customers;
using MonsterMart.Data;

namespace MonsterMart.Core
{
    /// <summary>当日结算数据 — 设计文档 §10.3 的结算界面字段。</summary>
    public struct DaySummary
    {
        public int day;
        public int salesRevenue;
        public int purchaseCost;
        public int costOfGoodsSold;
        public int spoilage;
        public int repairCost;
        public int profit;

        public int served;
        public int happy;
        public int leftAngry;
        public int leftUnserved;

        public int reputationBefore;
        public int reputationAfter;
        public float cleanliness;

        public bool goalsMet;
        public string goalReport;
        public string unlockNote;

        /// <summary>今天谁干了什么、现在多累 — 设计文档 §4.4。</summary>
        public string staffReport;
    }

    /// <summary>
    /// 营业日管理 — 设计文档 §12.1 DayManager：
    /// 当前天数、营业时间、顾客生成计划、当天事件、结算数据。
    /// </summary>
    public class DayManager : MonoBehaviour
    {
        public int CurrentDay { get; private set; } = 1;
        public DayPlan CurrentPlan { get; private set; }

        public float TimeRemaining { get; private set; }
        public float BusinessDuration { get; private set; }

        public int Served { get; private set; }
        public int Happy { get; private set; }
        public int LeftAngry { get; private set; }
        public int LeftUnserved { get; private set; }
        public int ShelvesKnockedOver { get; set; }

        public InspectionGrade InspectionResult { get; set; } = InspectionGrade.C;
        public bool InspectionDone { get; set; }

        /// <summary>今晚的预约条线索（营业前界面展示，玩家据此推断该进什么货）。</summary>
        public List<NightNote> Notes { get; private set; } = new List<NightNote>();

        int _reputationAtDayStart;

        public float ElapsedNormalized =>
            BusinessDuration <= 0f ? 0f : 1f - Mathf.Clamp01(TimeRemaining / BusinessDuration);

        public void SetDay(int day)
        {
            CurrentDay = Mathf.Clamp(day, 1, Mathf.Max(1, GameDatabase.DayCount));
            CurrentPlan = GameDatabase.GetDay(CurrentDay);
        }

        public void PrepareDay()
        {
            CurrentPlan = GameDatabase.GetDay(CurrentDay);
            BusinessDuration = CurrentPlan != null ? CurrentPlan.businessSeconds : 200f;
            TimeRemaining = BusinessDuration;
            Notes = NightNotes.Build(CurrentPlan);

            Served = 0;
            Happy = 0;
            LeftAngry = 0;
            LeftUnserved = 0;
            ShelvesKnockedOver = 0;
            InspectionDone = false;
            InspectionResult = InspectionGrade.C;

            _reputationAtDayStart = Game.Reputation != null ? Game.Reputation.Value : 0;
            Game.Economy?.ResetDaily();
        }

        public void BeginBusiness()
        {
            TimeRemaining = BusinessDuration;
            Game.Spawner?.BeginDay(CurrentPlan);
        }

        /// <summary>营业中每帧推进计时；返回 true 表示时间到了。</summary>
        public bool TickBusiness(float dt)
        {
            TimeRemaining -= dt;
            if (TimeRemaining > 0f) return false;

            TimeRemaining = 0f;
            return true;
        }

        public void RecordServed(CustomerController customer)
        {
            Served++;
            if (customer != null && customer.Satisfaction >= 70f) Happy++;
        }

        public void RecordLeftAngry(CustomerController customer) => LeftAngry++;
        public void RecordLeftUnserved(CustomerController customer) => LeftUnserved++;

        public bool IsLastDay => CurrentDay >= GameDatabase.DayCount;

        public void AdvanceDay() => SetDay(CurrentDay + 1);

        // ------------------------------------------------------------------
        // 结算
        // ------------------------------------------------------------------
        public DaySummary BuildSummary()
        {
            var eco = Game.Economy;
            var rep = Game.Reputation;
            var clean = Game.Cleanliness;

            var s = new DaySummary
            {
                day = CurrentDay,
                salesRevenue = eco != null ? eco.DaySalesRevenue : 0,
                purchaseCost = eco != null ? eco.DayPurchaseCost : 0,
                costOfGoodsSold = eco != null ? eco.DayCostOfGoodsSold : 0,
                spoilage = eco != null ? eco.DaySpoilage : 0,
                repairCost = eco != null ? eco.DayRepairCost : 0,
                profit = eco != null ? eco.DayProfit : 0,
                served = Served,
                happy = Happy,
                leftAngry = LeftAngry,
                leftUnserved = LeftUnserved,
                reputationBefore = _reputationAtDayStart,
                reputationAfter = rep != null ? rep.Value : 0,
                cleanliness = clean != null ? clean.Value : 0f,
            };

            var report = new List<string>();
            bool met = true;

            if (CurrentPlan != null)
            {
                if (CurrentPlan.goalCustomersServed > 0)
                {
                    bool ok = Served >= CurrentPlan.goalCustomersServed;
                    met &= ok;
                    report.Add($"{Mark(ok)} 服务顾客 {Served}/{CurrentPlan.goalCustomersServed}");
                }

                if (CurrentPlan.goalMinProfit > 0)
                {
                    bool ok = s.profit >= CurrentPlan.goalMinProfit;
                    met &= ok;
                    report.Add($"{Mark(ok)} 当日利润 {s.profit} / 需 ≥ {CurrentPlan.goalMinProfit}");
                }

                if (CurrentPlan.goalMinReputation > 0)
                {
                    bool ok = s.reputationAfter >= CurrentPlan.goalMinReputation;
                    met &= ok;
                    report.Add($"{Mark(ok)} 声望 {s.reputationAfter} / 需 ≥ {CurrentPlan.goalMinReputation}");
                }

                if (CurrentPlan.goalMinCleanliness > 0)
                {
                    bool ok = s.cleanliness >= CurrentPlan.goalMinCleanliness;
                    met &= ok;
                    report.Add($"{Mark(ok)} 整洁度 {Mathf.RoundToInt(s.cleanliness)} / 需 ≥ {CurrentPlan.goalMinCleanliness}");
                }

                // 狼人撞倒货架 —— ShelvesKnockedOver 一直在记，但以前没人读它，
                // 于是第二天简报里写着的「不让狼人破坏超过一个货架」从来没被判过。
                if (CurrentPlan.goalMaxShelvesKnocked >= 0)
                {
                    bool ok = ShelvesKnockedOver <= CurrentPlan.goalMaxShelvesKnocked;
                    met &= ok;
                    report.Add($"{Mark(ok)} 被撞倒的货架 {ShelvesKnockedOver} / 最多 {CurrentPlan.goalMaxShelvesKnocked}");
                }

                if (CurrentPlan.spawnInspector)
                {
                    bool ok = InspectionDone;
                    met &= ok;
                    report.Add($"{Mark(ok)} 检查员评价：{(InspectionDone ? InspectionResult.ToString() : "未完成")}");
                }
            }

            if (report.Count == 0) report.Add("（今天没有硬性目标）");

            s.goalsMet = met;
            s.goalReport = string.Join("\n", report);
            s.unlockNote = BuildUnlockNote();
            s.staffReport = BuildStaffReport();
            return s;
        }

        /// <summary>
        /// 今天谁干了什么、明天还剩几分力 — 设计文档 §4.4。
        ///
        /// 结算时才是玩家把「早上那次排班」和「今晚这个结果」对上号的时刻，
        /// 所以这一栏要写清楚谁连轴转了、谁已经累坏了。
        /// </summary>
        static string BuildStaffReport()
        {
            var roster = StaffRoster.All;
            if (roster.Count == 0) return "（还没有员工）";

            var lines = new List<string>();
            for (int i = 0; i < roster.Count; i++)
            {
                var entry = roster[i];
                if (entry.Data == null) continue;

                var duties = new List<string>();
                if (entry.onExpedition) duties.Add("远征");
                if (entry.nightJob != StaffAssignment.Rest)
                    duties.Add(StaffRoster.NightJobLabel(entry.nightJob));

                string duty = duties.Count > 0 ? string.Join(" + ", duties) : "休息";
                string tail = entry.IsDoubleShift ? "　<color=#F26B61>连轴转</color>" : "";

                lines.Add($"{entry.Data.displayName}　Lv.{entry.level}　{duty}　" +
                          $"{StaffRoster.FatigueLabel(entry)}{tail}");
            }

            return string.Join("\n", lines);
        }

        static string Mark(bool ok) => ok ? "✓" : "✗";

        string BuildUnlockNote()
        {
            var notes = new List<string>();

            var seen = BestiaryTracker.DiscoveredThisDay;
            for (int i = 0; i < seen.Count; i++)
                notes.Add($"图鉴解锁：{seen[i]}");

            if (Game.Store != null && Game.Store.Checkout.Level == 0 &&
                Game.Economy != null && Game.Economy.Money >= GameConfig.CheckoutUpgradeCost)
                notes.Add($"可以升级收银台了（{GameConfig.CheckoutUpgradeCost} 金币）");

            return notes.Count > 0 ? string.Join("\n", notes) : "（今晚没有新解锁）";
        }
    }
}
