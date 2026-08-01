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
    [System.Serializable]
    public class SaveData
    {
        public int version = GameConfig.SaveVersion;

        public int currentDay = 1;
        public int money;
        public int reputation;

        public List<string> unlockedProducts = new List<string>();
        public List<string> discoveredMonsters = new List<string>();

        public int checkoutLevel;

        public float sfxVolume = 0.55f;
        public float musicVolume = 0.22f;
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
            };

            // 原型里 8 种商品从第一天起全部可进货，这里如实记录以便将来扩展
            for (int i = 0; i < GameDatabase.Products.Count; i++)
                data.unlockedProducts.Add(GameDatabase.Products[i].productId);

            if (Game.Audio != null)
            {
                data.sfxVolume = Game.Audio.SfxVolume;
                data.musicVolume = Game.Audio.MusicVolume;
            }

            return data;
        }

        /// <summary>把存档套用到已经装配好的运行时对象上。</summary>
        public static void Apply(SaveData data)
        {
            if (data == null) return;

            Game.Day?.SetDay(data.currentDay);
            Game.Economy?.SetMoney(data.money);
            Game.Reputation?.SetValue(data.reputation);
            Game.Store?.Checkout.SetLevel(data.checkoutLevel);

            BestiaryTracker.LoadFromSaveList(data.discoveredMonsters);
            Game.Audio?.SetVolumes(data.sfxVolume, data.musicVolume);
        }
    }
}
