using MonsterMart.Customers;
using MonsterMart.Events;
using MonsterMart.Player;
using MonsterMart.Store;
using MonsterMart.UI;

namespace MonsterMart.Core
{
    /// <summary>
    /// 全局服务定位器。原型规模下比依赖注入更直接，
    /// 所有引用在 GameBootstrap 里一次性装配完成。
    /// </summary>
    public static class Game
    {
        public static GameManager Manager;
        public static DayManager Day;
        public static EconomyManager Economy;
        public static ReputationManager Reputation;
        public static CleanlinessManager Cleanliness;

        public static StoreWorld Store;
        public static PlayerController Player;
        public static CustomerSpawner Spawner;
        public static RandomEventManager Events;
        public static UIRoot UI;
        public static AudioDirector Audio;

        public static bool IsReady =>
            Manager != null && Store != null && Player != null && UI != null;

        public static void Clear()
        {
            Manager = null;
            Day = null;
            Economy = null;
            Reputation = null;
            Cleanliness = null;
            Store = null;
            Player = null;
            Spawner = null;
            Events = null;
            UI = null;
            Audio = null;
        }
    }
}
