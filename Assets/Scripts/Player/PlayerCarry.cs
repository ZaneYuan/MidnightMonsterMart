using UnityEngine;
using MonsterMart.Data;

namespace MonsterMart.Player
{
    /// <summary>
    /// 玩家携带的商品 — 设计文档 §3.1：
    /// 「原型阶段不要加入复杂背包。玩家一次只携带一种商品，最大携带数量 5。」
    /// </summary>
    public class PlayerCarry
    {
        public ProductData Product { get; private set; }
        public int Count { get; private set; }

        /// <summary>是否已经过灵界包装台处理（幽灵只收处理过的商品）。</summary>
        public bool Packed { get; private set; }

        public bool IsEmpty => Product == null || Count <= 0;
        public bool IsFull => Count >= GameConfig.PlayerCarryCapacity;

        public bool Has(ProductData product)
            => product != null && Product == product && Count > 0;

        public bool HasCleaningTool
            => Product != null && Product.isCleaningTool && Count > 0;

        /// <summary>拿起商品。返回实际拿到的数量。</summary>
        public int Take(ProductData product, int amount, bool packed = false)
        {
            if (product == null || amount <= 0) return 0;

            // 换成另一种商品时会先放下手上的
            if (Product != null && Product != product) Clear();

            Product = product;
            Packed = packed;
            int accepted = Mathf.Min(amount, GameConfig.PlayerCarryCapacity - Count);
            Count += accepted;
            return accepted;
        }

        public void Remove(int amount)
        {
            Count -= Mathf.Max(0, amount);
            if (Count <= 0) Clear();
        }

        public void Clear()
        {
            Product = null;
            Count = 0;
            Packed = false;
        }

        public int FreeSpace => GameConfig.PlayerCarryCapacity - Count;
    }
}
