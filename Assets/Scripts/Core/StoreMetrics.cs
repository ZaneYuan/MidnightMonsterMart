using System;
using UnityEngine;
using MonsterMart.Data;

// 设计文档 §6「店铺指标系统」的三个管理器。逻辑短小且高度相关，放在同一文件。
namespace MonsterMart.Core
{
    /// <summary>金钱与当日损益 — 设计文档 §6.1。</summary>
    public class EconomyManager : MonoBehaviour
    {
        public int Money { get; private set; }

        public int DaySalesRevenue { get; private set; }
        public int DayPurchaseCost { get; private set; }
        public int DaySpoilage { get; private set; }
        public int DayRepairCost { get; private set; }

        public event Action OnChanged;

        /// <summary>当日利润 = 销售收入 - 进货成本 - 商品损耗 - 维修支出。</summary>
        public int DayProfit => DaySalesRevenue - DayPurchaseCost - DaySpoilage - DayRepairCost;

        public void Initialize(int startingMoney)
        {
            Money = startingMoney;
            ResetDaily();
        }

        public void ResetDaily()
        {
            DaySalesRevenue = 0;
            DayPurchaseCost = 0;
            DaySpoilage = 0;
            DayRepairCost = 0;
            OnChanged?.Invoke();
        }

        public bool CanAfford(int amount) => Money >= amount;

        public bool TrySpend(int amount, bool asPurchase)
        {
            if (amount <= 0) return true;
            if (Money < amount) return false;

            Money -= amount;
            if (asPurchase) DayPurchaseCost += amount;
            else DayRepairCost += amount;

            OnChanged?.Invoke();
            return true;
        }

        public void RecordSale(int revenue)
        {
            if (revenue <= 0) return;
            Money += revenue;
            DaySalesRevenue += revenue;
            OnChanged?.Invoke();
        }

        public void RecordSpoilage(int amount)
        {
            if (amount <= 0) return;
            DaySpoilage += amount;
            OnChanged?.Invoke();
        }

        public void SetMoney(int value)
        {
            Money = Mathf.Max(0, value);
            OnChanged?.Invoke();
        }
    }

    /// <summary>店铺声望 0~100 — 设计文档 §6.2。</summary>
    public class ReputationManager : MonoBehaviour
    {
        public int Value { get; private set; }

        public event Action<int, string> OnChanged;

        public void Initialize(int starting)
        {
            Value = Mathf.Clamp(starting, GameConfig.ReputationMin, GameConfig.ReputationMax);
        }

        public void Add(int delta, string reason = null)
        {
            if (delta == 0) return;

            int before = Value;
            Value = Mathf.Clamp(Value + delta, GameConfig.ReputationMin, GameConfig.ReputationMax);

            if (Value != before) OnChanged?.Invoke(Value - before, reason);
        }

        public void SetValue(int value)
        {
            Value = Mathf.Clamp(value, GameConfig.ReputationMin, GameConfig.ReputationMax);
            OnChanged?.Invoke(0, null);
        }

        /// <summary>声望等级描述，结算界面用。</summary>
        public string Tier =>
            Value >= 80 ? "午夜名店" :
            Value >= 60 ? "口碑不错" :
            Value >= 40 ? "还算稳定" :
            Value >= 20 ? "岌岌可危" : "无人问津";
    }

    /// <summary>整洁度 0~100 — 设计文档 §6.3。</summary>
    public class CleanlinessManager : MonoBehaviour
    {
        public float Value { get; private set; }

        public event Action OnChanged;

        public void Initialize(float starting)
        {
            Value = Mathf.Clamp(starting, GameConfig.CleanlinessMin, GameConfig.CleanlinessMax);
        }

        public void Add(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;
            Value = Mathf.Clamp(Value + delta, GameConfig.CleanlinessMin, GameConfig.CleanlinessMax);
            OnChanged?.Invoke();
        }

        public void SetValue(float value)
        {
            Value = Mathf.Clamp(value, GameConfig.CleanlinessMin, GameConfig.CleanlinessMax);
            OnChanged?.Invoke();
        }

        public bool IsDirty => Value < GameConfig.CleanlinessDirtyThreshold;
        public bool IsFilthy => Value < GameConfig.CleanlinessFilthyThreshold;

        public string Tier =>
            Value >= 80 ? "一尘不染" :
            Value >= 60 ? "还算干净" :
            Value >= 40 ? "有点脏" :
            Value >= 20 ? "很脏" : "无法直视";
    }
}
