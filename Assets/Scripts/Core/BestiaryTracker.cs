using System.Collections.Generic;
using MonsterMart.Data;

namespace MonsterMart.Core
{
    /// <summary>
    /// 怪物图鉴解锁进度 — 设计文档 §2.1「检查怪物图鉴」与 §13 存档字段。
    /// </summary>
    public static class BestiaryTracker
    {
        static readonly HashSet<MonsterType> _discovered = new HashSet<MonsterType>();
        static readonly List<string> _discoveredThisDay = new List<string>();

        public static IReadOnlyCollection<MonsterType> Discovered => _discovered;
        public static IReadOnlyList<string> DiscoveredThisDay => _discoveredThisDay;

        public static bool IsDiscovered(MonsterType type) => _discovered.Contains(type);

        public static void Discover(MonsterType type)
        {
            if (!_discovered.Add(type)) return;

            var data = GameDatabase.GetCustomer(type);
            _discoveredThisDay.Add(data != null ? data.displayName : type.ToString());
        }

        public static void ClearDailyLog() => _discoveredThisDay.Clear();

        public static void Reset()
        {
            _discovered.Clear();
            _discoveredThisDay.Clear();
        }

        public static List<string> ToSaveList()
        {
            var list = new List<string>();
            foreach (var type in _discovered) list.Add(type.ToString());
            return list;
        }

        public static void LoadFromSaveList(List<string> list)
        {
            _discovered.Clear();
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (System.Enum.TryParse(list[i], out MonsterType type))
                    _discovered.Add(type);
            }
        }
    }
}
