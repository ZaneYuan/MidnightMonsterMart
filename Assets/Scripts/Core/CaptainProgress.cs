using UnityEngine;

namespace MonsterMart.Core
{
    /// <summary>
    /// 队长（玩家本人）的远征成长线 —— 和怪物员工的「打怪升级」是两条平行的线：
    /// 员工练等级涨战斗数值，队长练等级扩背包容量。用户反馈明确要求「总人物也可以
    /// 升级，升级后可以扩大背包容量」。
    ///
    /// 归属：<b>本局进度</b>，和金钱 / 冷藏货架核心同级（走 SaveSystem.Apply 的
    /// includeRunProgress 分支）。重开一局清空，续玩恢复。
    /// </summary>
    public static class CaptainProgress
    {
        public static int Level { get; private set; } = 1;
        public static float Xp { get; private set; }

        /// <summary>等级上限，和 StaffRoster.MaxLevel 一样刻意压低。</summary>
        public const int MaxLevel = 5;

        /// <summary>每级多出来的携带容量。</summary>
        public const int CapacityPerLevel = 4;

        public static float XpToNext(int level) => 50f * level;

        /// <summary>算上等级之后，比基础携带容量多出来的量。</summary>
        public static int CapacityBonus => (Level - 1) * CapacityPerLevel;

        /// <summary>加经验，够了就升级（可能连跳几级）。返回是否升级了。</summary>
        public static bool AddXp(float amount)
        {
            if (amount <= 0f || Level >= MaxLevel) return false;

            Xp += amount;
            bool leveled = false;

            while (Level < MaxLevel && Xp >= XpToNext(Level))
            {
                Xp -= XpToNext(Level);
                Level++;
                leveled = true;
            }

            if (Level >= MaxLevel) Xp = 0f;   // 封顶后不再囤经验条
            return leveled;
        }

        /// <summary>重开一局时清空。</summary>
        public static void Reset()
        {
            Level = 1;
            Xp = 0f;
        }

        public static void LoadFromSave(int level, float xp)
        {
            Level = Mathf.Clamp(level <= 0 ? 1 : level, 1, MaxLevel);
            Xp = Mathf.Max(0f, xp);
        }
    }
}
