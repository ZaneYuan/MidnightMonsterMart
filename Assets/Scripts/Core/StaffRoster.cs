using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Data;

namespace MonsterMart.Core
{
    /// <summary>
    /// 员工名册与当日排班 — 设计文档 §4「怪物员工双岗位系统」。
    ///
    /// §4.1 的核心规则是「玩家每天必须从有限人手中安排远征与夜班」，
    /// 而 §4.4 补上了那条张力：「白天远征后继续值夜班会快速累积疲劳，
    /// 过高时降低效率并触发失误」。
    ///
    /// 所以<b>出征</b>和<b>夜班岗位</b>是两个独立的轴：
    /// 同一只怪物可以既出征又站收银台，代价是第二天基本废掉。
    /// 「让强力员工出征还是留店」这个决策就建立在这上面。
    ///
    /// 归属：<b>本局进度</b>，和金钱同级 —— 走 SaveSystem.Apply 的
    /// includeRunProgress 分支，开新局清空。
    /// </summary>
    public static class StaffRoster
    {
        /// <summary>一名员工的当日状态。</summary>
        public class Entry
        {
            public string staffId;

            /// <summary>今天进远征队 — §3.3「上阵 3 名怪物员工」。</summary>
            public bool onExpedition;

            /// <summary>今晚的店内岗位 — §4.3。</summary>
            public StaffAssignment nightJob = StaffAssignment.Rest;

            /// <summary>疲劳 0~100 — §4.4。</summary>
            public float fatigue;

            /// <summary>打怪升级：战斗等级，从 1 开始。</summary>
            public int level = 1;

            /// <summary>朝下一级累积的经验值。</summary>
            public float xp;

            /// <summary>白天出征、晚上还上岗 —— §4.4 说的就是这种人。</summary>
            public bool IsDoubleShift => onExpedition && nightJob != StaffAssignment.Rest;

            /// <summary>今天什么都不干，可以回血。</summary>
            public bool IsFullyResting => !onExpedition && nightJob == StaffAssignment.Rest;

            public StaffData Data => GameDatabase.GetStaff(staffId);
        }

        static readonly List<Entry> _entries = new List<Entry>();

        public static IReadOnlyList<Entry> All { get { EnsureBuilt(); return _entries; } }

        // ---------- 数值（§4.4 疲劳） ----------
        public const float MaxFatigue = 100f;

        /// <summary>出征一趟的疲劳。</summary>
        public const float ExpeditionFatigue = 34f;

        /// <summary>值一晚夜班的疲劳。两者叠起来 56，两天连轴转就接近累坏。</summary>
        public const float NightShiftFatigue = 22f;

        /// <summary>整天什么都不干回复的疲劳。</summary>
        public const float RestRecovery = 45f;

        /// <summary>疲劳拉满时效率剩多少 —— 不归零，否则排班变成「只能休息」。</summary>
        public const float MinEfficiency = 0.35f;

        /// <summary>疲劳高于这个值就算「累坏了」，界面标红。</summary>
        public const float ExhaustedThreshold = 70f;

        /// <summary>上阵人数上限 — §3.3「上阵 3 名怪物员工」。</summary>
        public const int MaxSquadSize = 3;

        // ---------- 数值（打怪升级） ----------
        /// <summary>
        /// 等级上限。三天流程里刻意压低 —— 全勤出征也就摸到中间档，
        /// 但每一级都要能感觉出来，不是刷个几十级才见效的那种曲线。
        /// </summary>
        public const int MaxLevel = 6;

        const float DamagePerLevel = 0.12f;
        const float HealthPerLevel = 0.15f;

        /// <summary>升到 level+1 需要的经验值。</summary>
        public static float XpToNext(int level) => 40f * level;

        public static float DamageMultiplier(Entry entry)
            => entry == null ? 1f : 1f + DamagePerLevel * (entry.level - 1);

        public static float HealthMultiplier(Entry entry)
            => entry == null ? 1f : 1f + HealthPerLevel * (entry.level - 1);

        /// <summary>
        /// 加经验，够了就升级（可能连跳几级）。返回是否升级了 ——
        /// 界面和远征里的提示都靠这个判断要不要弹「升级了」。
        /// </summary>
        public static bool AddXp(string staffId, float amount)
        {
            var entry = Get(staffId);
            if (entry == null || amount <= 0f || entry.level >= MaxLevel) return false;

            entry.xp += amount;
            bool leveled = false;

            while (entry.level < MaxLevel && entry.xp >= XpToNext(entry.level))
            {
                entry.xp -= XpToNext(entry.level);
                entry.level++;
                leveled = true;
            }

            if (entry.level >= MaxLevel) entry.xp = 0f;   // 封顶后不再囤经验条
            return leveled;
        }

        static void EnsureBuilt()
        {
            if (_entries.Count > 0) return;

            var staff = GameDatabase.Staff;
            for (int i = 0; i < staff.Count; i++)
                _entries.Add(new Entry { staffId = staff[i].staffId });

            ApplyDefaultAssignments();
        }

        /// <summary>
        /// 默认排班：前三个出征，第四个站收银。
        /// 玩家什么都不改也能玩下去，而且这正好是最直觉的配置 ——
        /// 等他发现「留店的那个人一个人管不过来」，取舍就自己浮出来了。
        /// </summary>
        static void ApplyDefaultAssignments()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].onExpedition = i < MaxSquadSize;
                _entries[i].nightJob = i < MaxSquadSize
                    ? StaffAssignment.Rest
                    : StaffAssignment.Cashier;
            }
        }

        public static Entry Get(string staffId)
        {
            EnsureBuilt();
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].staffId == staffId) return _entries[i];
            return null;
        }

        // ------------------------------------------------------------------
        // 排班
        // ------------------------------------------------------------------
        /// <summary>
        /// 把某人拉进 / 踢出远征队。返回操作后他是否在队里 ——
        /// 队伍满员（§3.3 上阵 3 名）时想进会被拒，返回 false。
        /// </summary>
        public static bool SetOnExpedition(string staffId, bool onExpedition)
        {
            var entry = Get(staffId);
            if (entry == null) return false;
            if (entry.onExpedition == onExpedition) return onExpedition;

            if (onExpedition && SquadSize >= MaxSquadSize) return false;

            entry.onExpedition = onExpedition;
            return entry.onExpedition;
        }

        public static bool ToggleExpedition(string staffId)
        {
            var entry = Get(staffId);
            if (entry == null) return false;
            return SetOnExpedition(staffId, !entry.onExpedition);
        }

        public static void SetNightJob(string staffId, StaffAssignment job)
        {
            var entry = Get(staffId);
            if (entry != null) entry.nightJob = job;
        }

        /// <summary>轮换夜班岗位（界面上点一下换一个岗）。</summary>
        public static StaffAssignment CycleNightJob(string staffId)
        {
            var entry = Get(staffId);
            if (entry == null) return StaffAssignment.Rest;

            var order = new[]
            {
                StaffAssignment.Rest,
                StaffAssignment.Cashier,
                StaffAssignment.Restock,
                StaffAssignment.Security,
            };

            int start = System.Array.IndexOf(order, entry.nightJob);
            entry.nightJob = order[(start + 1) % order.Length];
            return entry.nightJob;
        }

        public static int SquadSize
        {
            get
            {
                EnsureBuilt();
                int n = 0;
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].onExpedition) n++;
                return n;
            }
        }

        public static int CountOnNightJob(StaffAssignment job)
        {
            EnsureBuilt();
            int n = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].nightJob == job) n++;
            return n;
        }

        /// <summary>今天出征的员工 id，按名册顺序。</summary>
        public static string[] ExpeditionSquad()
        {
            EnsureBuilt();
            var ids = new List<string>();
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].onExpedition) ids.Add(_entries[i].staffId);
            return ids.ToArray();
        }

        /// <summary>某个岗位上的第一个人（原型阶段一个岗位只认一个人）。</summary>
        public static Entry FirstOn(StaffAssignment job)
        {
            EnsureBuilt();
            if (job == StaffAssignment.Rest) return null;

            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].nightJob == job) return _entries[i];
            return null;
        }

        public static bool AnyOn(StaffAssignment job) => FirstOn(job) != null;

        // ------------------------------------------------------------------
        // 疲劳 — 设计文档 §4.4
        // ------------------------------------------------------------------
        /// <summary>
        /// 岗位效率：MinEfficiency ~ 1，疲劳越高越低。
        /// 没人在这个岗位上就是 0（= 这个岗位今晚没人管）。
        /// </summary>
        public static float EfficiencyOn(StaffAssignment job)
        {
            var entry = FirstOn(job);
            return entry == null ? 0f : Efficiency(entry);
        }

        public static float Efficiency(Entry entry)
        {
            if (entry == null) return 0f;
            float t = Mathf.Clamp01(entry.fatigue / MaxFatigue);
            return Mathf.Lerp(1f, MinEfficiency, t);
        }

        public static bool IsExhausted(Entry entry)
            => entry != null && entry.fatigue >= ExhaustedThreshold;

        public static void AddFatigue(string staffId, float amount)
        {
            var entry = Get(staffId);
            if (entry == null) return;
            entry.fatigue = Mathf.Clamp(entry.fatigue + amount, 0f, MaxFatigue);
        }

        /// <summary>
        /// 出征回来时结算远征疲劳。刻意和夜班分开算 ——
        /// 「白天出征 + 晚上值夜班」要吃两份，这正是 §4.4 想让玩家感觉到的东西。
        /// </summary>
        public static void ApplyExpeditionFatigue()
        {
            EnsureBuilt();
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].onExpedition)
                    AddFatigue(_entries[i].staffId, ExpeditionFatigue);
        }

        /// <summary>
        /// 一天结束时结算夜班疲劳与休息回复。
        ///
        /// 只有「今天彻底没干活」的人才回血：白天跑了一趟远征、晚上才休息的，
        /// 那份远征疲劳得留着 —— 否则出征就没有第二天的代价了。
        /// </summary>
        public static void ApplyNightShiftFatigue()
        {
            EnsureBuilt();
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                if (entry.nightJob != StaffAssignment.Rest)
                    AddFatigue(entry.staffId, NightShiftFatigue);
                else if (entry.IsFullyResting)
                    AddFatigue(entry.staffId, -RestRecovery);
            }
        }

        /// <summary>一句话说明某人现在的状态，给晨会界面用。</summary>
        public static string FatigueLabel(Entry entry)
        {
            if (entry == null) return "";
            if (entry.fatigue >= ExhaustedThreshold) return "累坏了";
            if (entry.fatigue >= 40f) return "有点累";
            return "精神好";
        }

        public static string NightJobLabel(StaffAssignment job)
        {
            switch (job)
            {
                case StaffAssignment.Cashier: return "收银";
                case StaffAssignment.Restock: return "补货";
                case StaffAssignment.Security: return "安保";
                default: return "不值班";
            }
        }

        // ------------------------------------------------------------------
        // 生命周期与存档
        // ------------------------------------------------------------------
        /// <summary>重开一局时清空（排班回默认、疲劳归零）。</summary>
        public static void Reset() => _entries.Clear();

        public static List<string> ToSaveList()
        {
            EnsureBuilt();
            var list = new List<string>();
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                list.Add($"{e.staffId}|{(e.onExpedition ? 1 : 0)}|{(int)e.nightJob}|" +
                         $"{Mathf.RoundToInt(e.fatigue)}|{e.level}|{e.xp:0.##}");
            }
            return list;
        }

        /// <summary>
        /// 从存档恢复。格式是 `id|是否出征|夜班岗位|疲劳|等级|经验`，
        /// 空列表 = 旧存档 = 用默认排班，和加字段之前的行为一致。
        /// 等级/经验是后加的两段，旧存档没有就退回 1 级 0 经验。
        /// </summary>
        public static void LoadFromSaveList(List<string> list)
        {
            Reset();
            EnsureBuilt();
            if (list == null || list.Count == 0) return;

            for (int i = 0; i < list.Count; i++)
            {
                var parts = list[i].Split('|');
                if (parts.Length < 4) continue;

                var entry = Get(parts[0]);
                if (entry == null) continue;

                entry.onExpedition = parts[1] == "1";

                if (int.TryParse(parts[2], out int job) &&
                    System.Enum.IsDefined(typeof(StaffAssignment), job))
                    entry.nightJob = (StaffAssignment)job;

                if (float.TryParse(parts[3], out float fatigue))
                    entry.fatigue = Mathf.Clamp(fatigue, 0f, MaxFatigue);

                if (parts.Length >= 6)
                {
                    if (int.TryParse(parts[4], out int level))
                        entry.level = Mathf.Clamp(level, 1, MaxLevel);
                    if (float.TryParse(parts[5], out float xp))
                        entry.xp = Mathf.Max(0f, xp);
                }
            }
        }
    }
}
