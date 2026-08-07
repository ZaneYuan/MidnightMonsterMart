using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MonsterMart.Data;

namespace MonsterMart.Core
{
    /// <summary>
    /// 存档数据 — 设计文档 §13。
    /// 「原型仅需要保存：当前天数、金钱、声望、已解锁商品、已发现怪物、店铺升级、音量设置。」
    /// </summary>
    /// <summary>一条库存记录。JsonUtility 不支持字典，所以按商品 id 展成列表。</summary>
    [System.Serializable]
    public class StockEntry
    {
        public string productId;
        public int count;

        public StockEntry() { }

        public StockEntry(string productId, int count)
        {
            this.productId = productId;
            this.count = count;
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public int version = GameConfig.SaveVersion;

        public int currentDay = 1;
        public int money;
        public int reputation;

        /// <summary>
        /// 跨天累计利润。结局判定看的是这个而不是当日利润
        /// （GameManager.EvaluateEnding 拿它和 EndingExcellentProfit 比）。
        ///
        /// 纯新增字段，不 bump SaveVersion：JsonUtility 读老存档时这一项
        /// 取默认值 0，行为和修复前一致，没必要把玩家进行中的存档作废。
        /// </summary>
        public int totalProfit;

        /// <summary>
        /// 这一局是不是已经打完了（走到过 GameOver）。
        ///
        /// 终局存档记的 currentDay 仍然是最后一天，没有这个标记的话重进会被
        /// 当成「第三天的进度」恢复，玩家永远被丢回第三天重打、看不到结局；
        /// 结算界面的「再开一局」走的也是同一条 BootGame 路径，同样开不了新局。
        ///
        /// 同样是纯新增字段，不 bump SaveVersion：旧存档读出来是 false，
        /// 也就是「未通关、照常续玩」，和修复前行为一致。
        /// </summary>
        public bool runCompleted;

        /// <summary>
        /// 存档的那一刻停在结算界面 —— 也就是 currentDay 这一天已经打完、
        /// 它的利润也已经计入 totalProfit。
        ///
        /// 没有这个标记的话，玩家在结算界面退出再进来会被恢复成「currentDay 的
        /// 准备阶段」，重打一遍当天，结算时把当日利润再加进 totalProfit 一次。
        ///
        /// 同样是纯新增字段，不 bump SaveVersion：旧存档读出来是 false，
        /// 也就是「停在准备阶段 / 营业中」，和修复前行为一致。
        /// </summary>
        public bool daySettled;

        public List<string> unlockedProducts = new List<string>();
        public List<string> discoveredMonsters = new List<string>();

        public int checkoutLevel;

        /// <summary>
        /// 仓库与货架库存 — 设计文档 §15 要求存档保存「仓库商品」。
        ///
        /// 以前没存：玩家在准备阶段花钱进的货，重进后凭空消失，钱却已经扣掉了
        /// （结算存档记的是花完钱之后的余额），等于净亏一笔。
        /// 空列表 = 仓库和货架都是空的，正好是旧存档的默认行为。
        /// </summary>
        public List<StockEntry> warehouse = new List<StockEntry>();

        /// <summary>货架上的存货。每个货架绑定唯一商品，所以按商品 id 索引即可。</summary>
        public List<StockEntry> shelfStock = new List<StockEntry>();

        /// <summary>
        /// 远征侧的关键设施材料与地区解锁 —— 设计文档 §3.4「Boss 房：掉落关键设施材料
        /// 并解锁下一地区」。属于<b>本局进度</b>，和金钱同级，重开一局要清掉。
        ///
        /// 同样是纯新增字段，不 bump SaveVersion：旧存档读出来是 0 / 空列表，
        /// 也就是「还没打过 Boss」，和加字段之前的行为一致。
        /// </summary>
        public int coldShelfCores;

        public List<string> unlockedRegions = new List<string>();

        /// <summary>
        /// 员工排班与疲劳 — 设计文档 §4.1 / §4.4。每条是 `id|分工|疲劳`。
        ///
        /// 同样是纯新增字段，不 bump SaveVersion：旧存档读出来是空列表，
        /// StaffRoster 会退回默认排班，和加字段之前的行为一致。
        /// </summary>
        public List<string> staffRoster = new List<string>();

        /// <summary>
        /// 存档的那一刻，今天那趟远征已经用掉了。
        ///
        /// 一天只有一趟远征（§2.1），而一趟要跑五六分钟。没有这个标记的话，
        /// 远征回来存的档重进后会退回晨会，玩家要么白跑一趟、要么带着已经到手的
        /// 战利品再去一趟 —— 两种都不对。
        ///
        /// 同样是纯新增字段，不 bump SaveVersion：旧存档读出来是 false，
        /// 也就是「今天还没出门」，和加字段之前一致。
        /// </summary>
        public bool expeditionDoneToday;

        /// <summary>
        /// 队长（玩家本人）的远征成长线 —— 升级扩背包容量，和怪物员工的等级是两回事。
        /// 归属：本局进度，和冷藏货架核心同级。纯新增字段，不 bump SaveVersion：
        /// 旧存档读出来是 0，CaptainProgress.LoadFromSave 会把它归一到 1 级 0 经验。
        /// </summary>
        public int captainLevel;
        public float captainXp;

        public float sfxVolume = 0.55f;
        public float musicVolume = 0.22f;

        /// <summary>
        /// 玩家选的营业倍速 —— 和音量一样是跨局累积的偏好设置，不属于本局进度。
        /// 旧存档没有这个字段：JsonUtility 只覆盖 JSON 里出现过的字段，缺省的
        /// 字段保留这里声明的初始值，所以旧存档读出来就是 1x，不会是 0。
        /// </summary>
        public float businessSpeed = 1f;
    }

    /// <summary>
    /// JSON 存档 — 设计文档 §13：
    /// 存到 Application.persistentDataPath，不使用 PlayerPrefs 保存完整进度。
    /// </summary>
    public static class SaveSystem
    {
        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, GameConfig.SaveFileName);

        public static bool Exists => File.Exists(FilePath);

        public static void Save()
        {
            try
            {
                var data = Capture();
                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(FilePath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveSystem] 保存失败：{e.Message}");
            }
        }

        public static SaveData Load()
        {
            try
            {
                if (!Exists) return null;

                var json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null || data.version != GameConfig.SaveVersion)
                {
                    Debug.Log("[SaveSystem] 存档版本不匹配，忽略旧存档。");
                    return null;
                }

                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveSystem] 读取失败：{e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (Exists) File.Delete(FilePath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveSystem] 删除失败：{e.Message}");
            }
        }

        static SaveData Capture()
        {
            var data = new SaveData
            {
                currentDay = Game.Day != null ? Game.Day.CurrentDay : 1,
                money = Game.Economy != null ? Game.Economy.Money : GameConfig.StartingMoney,
                reputation = Game.Reputation != null ? Game.Reputation.Value : GameConfig.StartingReputation,
                checkoutLevel = Game.Store != null ? Game.Store.Checkout.Level : 0,
                discoveredMonsters = BestiaryTracker.ToSaveList(),
                totalProfit = Game.Manager != null ? Game.Manager.TotalProfit : 0,
                runCompleted = Game.Manager != null && Game.Manager.State == GameState.GameOver,
                daySettled = Game.Manager != null && Game.Manager.State == GameState.Settlement,
                coldShelfCores = ExpeditionProgress.ColdShelfCores,
                unlockedRegions = ExpeditionProgress.ToSaveList(),
                staffRoster = StaffRoster.ToSaveList(),
                expeditionDoneToday = Game.Manager != null && Game.Manager.ExpeditionDoneToday,
                captainLevel = CaptainProgress.Level,
                captainXp = CaptainProgress.Xp,
            };

            // 原型里 8 种商品从第一天起全部可进货，这里如实记录以便将来扩展
            for (int i = 0; i < GameDatabase.Products.Count; i++)
                data.unlockedProducts.Add(GameDatabase.Products[i].productId);

            CaptureStock(data);

            if (Game.Audio != null)
            {
                data.sfxVolume = Game.Audio.SfxVolume;
                data.musicVolume = Game.Audio.MusicVolume;
            }

            if (Game.Manager != null) data.businessSpeed = Game.Manager.BusinessSpeed;

            return data;
        }

        /// <summary>
        /// 这份存档该不该被当成「进行中的进度」恢复。
        /// 已经通关的那一局不算 —— 否则重进会被丢回最后一天反复重打。
        /// </summary>
        public static bool ShouldResume(SaveData data) => data != null && !data.runCompleted;

        /// <summary>
        /// 这次启动该不该续玩。<paramref name="freshRun"/> 是玩家主动点了暂停菜单的
        /// 「重新开始」—— 本局进度作废，哪怕这一局还没打完；
        /// 图鉴和音量走 Apply 的跨局分支，照样保留。
        /// </summary>
        public static bool ShouldResume(SaveData data, bool freshRun)
            => !freshRun && ShouldResume(data);

        /// <summary>
        /// 该从哪一天接着玩。存档是停在结算界面存的（daySettled）就说明那一天
        /// 已经打完、利润也已经算进 totalProfit 了，接着玩的是下一天 ——
        /// 否则会重打当天，并把当日利润再累计一遍。
        /// </summary>
        public static int ResumeDay(SaveData data)
        {
            if (data == null) return 1;
            return data.daySettled ? data.currentDay + 1 : data.currentDay;
        }

        /// <summary>
        /// 续玩时该不该跳过晨会、直接落到闭店准备 —— 也就是「今天那趟远征已经跑完了」。
        ///
        /// daySettled 的存档记的是「这一天已经打完」，ResumeDay 会把玩家送到<b>下一天</b>，
        /// 而那一天的远征当然还没用过，所以这里必须把它排除掉。
        /// </summary>
        public static bool ShouldResumeAfterExpedition(SaveData data)
            => data != null && data.expeditionDoneToday && !data.daySettled;

        /// <summary>
        /// 把存档套用到已经装配好的运行时对象上。
        ///
        /// <paramref name="includeRunProgress"/> = false 时只恢复跨局累积的东西
        /// （图鉴、音量），本局进度（天数 / 金钱 / 声望 / 收银台等级）留在初始值，
        /// 给「上一局已通关，这次从第一天重开」用。
        ///
        /// 注意 totalProfit 不在这里恢复：GameBootstrap 是先 Apply、再
        /// StartNewRun，而 StartNewRun 会重置累计利润，写在这里会被它冲掉。
        /// 它作为参数传给 StartNewRun。
        /// </summary>
        public static void Apply(SaveData data, bool includeRunProgress = true)
        {
            if (data == null) return;

            // 跨局累积：不管上一局有没有打完都要恢复
            BestiaryTracker.LoadFromSaveList(data.discoveredMonsters);
            Game.Audio?.SetVolumes(data.sfxVolume, data.musicVolume);
            Game.Manager?.SetBusinessSpeed(data.businessSpeed > 0f ? data.businessSpeed : 1f);

            if (!includeRunProgress)
            {
                // 开新局：冷藏货架核心、地区解锁、排班与疲劳、队长等级都属于本局进度，
                // 和金钱一样丢弃
                ExpeditionProgress.Reset();
                StaffRoster.Reset();
                CaptainProgress.Reset();
                return;
            }

            Game.Day?.SetDay(ResumeDay(data));
            Game.Economy?.SetMoney(data.money);
            Game.Reputation?.SetValue(data.reputation);
            Game.Store?.Checkout.SetLevel(data.checkoutLevel);
            ExpeditionProgress.LoadFromSave(data.coldShelfCores, data.unlockedRegions);
            StaffRoster.LoadFromSaveList(data.staffRoster);
            CaptainProgress.LoadFromSave(data.captainLevel, data.captainXp);

            ApplyStock(data);
        }

        // ------------------------------------------------------------------
        // 库存
        // ------------------------------------------------------------------
        static void CaptureStock(SaveData data)
        {
            var store = Game.Store;
            if (store == null) return;

            foreach (var pair in store.Warehouse)
            {
                if (pair.Key == null || pair.Value <= 0) continue;
                data.warehouse.Add(new StockEntry(pair.Key.productId, pair.Value));
            }

            for (int i = 0; i < store.Shelves.Count; i++)
            {
                var shelf = store.Shelves[i];
                if (shelf == null || shelf.product == null || shelf.count <= 0) continue;
                data.shelfStock.Add(new StockEntry(shelf.product.productId, shelf.count));
            }
        }

        static void ApplyStock(SaveData data)
        {
            var store = Game.Store;
            if (store == null) return;

            // 先清干净，再按存档写回 —— 否则 Build() 之后残留的初始值会叠加上去
            for (int i = 0; i < GameDatabase.Products.Count; i++)
                store.Warehouse[GameDatabase.Products[i]] = 0;

            if (data.warehouse != null)
            {
                for (int i = 0; i < data.warehouse.Count; i++)
                {
                    var product = GameDatabase.GetProduct(data.warehouse[i].productId);
                    if (product == null) continue;
                    store.Warehouse[product] = Mathf.Max(0, data.warehouse[i].count);
                }
            }

            for (int i = 0; i < store.Shelves.Count; i++)
            {
                var shelf = store.Shelves[i];
                if (shelf == null) continue;
                shelf.count = 0;
                shelf.Refresh();
            }

            if (data.shelfStock == null) return;

            for (int i = 0; i < data.shelfStock.Count; i++)
            {
                var product = GameDatabase.GetProduct(data.shelfStock[i].productId);
                if (product == null) continue;

                var shelf = store.FindShelf(product);
                if (shelf == null) continue;

                shelf.count = Mathf.Clamp(data.shelfStock[i].count, 0, shelf.capacity);
                shelf.Refresh();
            }
        }
    }
}
