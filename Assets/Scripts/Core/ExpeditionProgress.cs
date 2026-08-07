using System.Collections.Generic;

namespace MonsterMart.Core
{
    /// <summary>
    /// 远征侧的长期产出 — 设计文档 §3.4「Boss 房：结算区域机制，掉落关键设施材料
    /// 并解锁下一地区」与 §3.5「区域 Boss：孢子巨兽；击败后获得冷藏货架核心」。
    ///
    /// 冷藏货架核心<b>不是商品</b>：它进不了货架、也卖不掉，所以刻意不做成
    /// ProductData —— 否则它会出现在备货界面的进货列表里变成「花 0 元能买的货」。
    /// 这里按 §2.1 阶段六「使用利润和异界材料扩建商店」的定位单独记账。
    ///
    /// 归属：<b>本局进度</b>，和金钱 / 声望同级（走 SaveSystem.Apply 的
    /// includeRunProgress 分支）。重开一局要从暮光森林重新打起，
    /// 不像图鉴那样跨局累积。
    /// </summary>
    public static class ExpeditionProgress
    {
        /// <summary>已获得的冷藏货架核心数量。</summary>
        public static int ColdShelfCores { get; private set; }

        static readonly HashSet<string> _unlockedRegions = new HashSet<string>();

        /// <summary>已解锁的供货区域 id。暮光森林是起点，不需要解锁。</summary>
        public static IReadOnlyCollection<string> UnlockedRegions => _unlockedRegions;

        public static bool IsRegionUnlocked(string regionId)
            => !string.IsNullOrEmpty(regionId) && _unlockedRegions.Contains(regionId);

        public static void AddColdShelfCores(int amount)
        {
            if (amount <= 0) return;
            ColdShelfCores += amount;
        }

        /// <summary>解锁一个新区域。返回 true 表示这次才刚解锁（用来决定要不要报喜）。</summary>
        public static bool UnlockRegion(string regionId)
            => !string.IsNullOrEmpty(regionId) && _unlockedRegions.Add(regionId);

        /// <summary>重开一局时清空。</summary>
        public static void Reset()
        {
            ColdShelfCores = 0;
            _unlockedRegions.Clear();
        }

        public static List<string> ToSaveList() => new List<string>(_unlockedRegions);

        public static void LoadFromSave(int coldShelfCores, List<string> regions)
        {
            ColdShelfCores = coldShelfCores < 0 ? 0 : coldShelfCores;

            _unlockedRegions.Clear();
            if (regions == null) return;

            for (int i = 0; i < regions.Count; i++)
                if (!string.IsNullOrEmpty(regions[i])) _unlockedRegions.Add(regions[i]);
        }
    }
}
