using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Player;

namespace MonsterMart.Store
{
    /// <summary>
    /// 仓库门 — 设计文档 §3.3 的补货流程起点。
    /// 玩家靠近 → 选择商品 → 携带（一次只带一种，上限 5 件）。
    /// </summary>
    public class StockRoom : Interactable
    {
        public CellRect cells;

        public void Configure(CellRect rect)
        {
            cells = rect;
            transform.position = rect.CenterWorld;

            var go = new GameObject("Door");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Panel(new Color(0.42f, 0.32f, 0.24f),
                                            new Color(0.24f, 0.18f, 0.14f),
                                            rect.WidthCells, rect.HeightCells);
            sr.sortingOrder = SortingLayers.Fixture;

            var sign = new GameObject("Sign");
            sign.transform.SetParent(transform, false);
            var signSr = sign.AddComponent<SpriteRenderer>();
            signSr.sprite = SpriteFactory.Solid(new Color(0.85f, 0.78f, 0.45f));
            signSr.sortingOrder = SortingLayers.FixtureOverlay;
            signSr.transform.localScale = new Vector3(0.7f, 0.25f, 1f);
            signSr.transform.localPosition = new Vector3(0f, -cells.HeightCells * 0.5f + 0.2f, 0f);
        }

        public override Vector2 InteractAnchor => cells.CenterWorld;

        public override bool IsAvailable(PlayerController player)
        {
            if (Game.Manager == null) return false;
            // 营业中和营业前都能取货；结算 / 暂停时不行
            return Game.Manager.State == GameState.Open ||
                   Game.Manager.State == GameState.Preparation;
        }

        public override string GetPrompt(PlayerController player)
        {
            if (player != null && !player.Carry.IsEmpty)
                return $"[E] 打开仓库（当前携带 {player.Carry.Product.displayName} ×{player.Carry.Count}）";
            return "[E] 打开仓库取货";
        }

        public override void OnInteract(PlayerController player)
        {
            Game.Audio?.PlayUiClick();
            Game.UI?.ShowStockRoom();
        }
    }
}
