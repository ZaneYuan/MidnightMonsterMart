using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Customers;
using MonsterMart.Data;
using MonsterMart.Player;

namespace MonsterMart.Store
{
    /// <summary>
    /// 收银台 — 设计文档 §5。
    /// 负责排队点管理、收银会话的开启，扫描玩法本身在 CheckoutView 里。
    /// </summary>
    public class Checkout : FixtureInteractable
    {
        /// <summary>收银台等级：0 = 初级，1 = 升级（文档 §5.2）。</summary>
        public int Level { get; private set; }

        readonly List<CustomerController> _queue = new List<CustomerController>();
        readonly List<Vector2Int> _queuePoints = new List<Vector2Int>();

        public IReadOnlyList<CustomerController> Queue => _queue;
        public int QueueLength => _queue.Count;

        /// <summary>正在结账的顾客（null 表示没人在收银）。</summary>
        public CustomerController ActiveCustomer { get; private set; }

        public bool SessionOpen => ActiveCustomer != null;

        /// <summary>
        /// 收银岗的效率（0 = 今晚没排人）— 设计文档 §4.3
        /// 「收银：决定结账速度、错误率和排队耐心」。
        /// </summary>
        public static float CashierEfficiency =>
            StaffRoster.EfficiencyOn(StaffAssignment.Cashier);

        /// <summary>扫描判定区。收银台等级决定基线，收银岗的员工在此之上加宽。</summary>
        public float ScanWindow =>
            (Level >= 1 ? GameConfig.ScanUpgradedWindow : GameConfig.ScanBaseWindow) *
            (1f + GameConfig.CashierScanBonus * CashierEfficiency);

        /// <summary>排队额外掉耐心的倍率。排了收银岗就有人分担，队伍没那么焦躁。</summary>
        public float QueuePatienceMultiplier =>
            (Level >= 1 ? GameConfig.UpgradedQueuePatienceMultiplier : 1f) *
            (1f - GameConfig.CashierQueueRelief * CashierEfficiency);

        SpriteRenderer _scannerLight;

        public void Configure(CellRect rect, IEnumerable<Vector2Int> queuePoints)
        {
            cells = rect;
            transform.position = rect.CenterWorld;

            _queuePoints.Clear();
            _queuePoints.AddRange(queuePoints);

            var body = new GameObject("Counter");
            body.transform.SetParent(transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Panel(new Color(0.46f, 0.40f, 0.30f),
                                            new Color(0.26f, 0.22f, 0.16f),
                                            rect.WidthCells, rect.HeightCells);
            sr.sortingOrder = SortingLayers.Fixture;

            var lightGo = new GameObject("ScannerLight");
            lightGo.transform.SetParent(transform, false);
            _scannerLight = lightGo.AddComponent<SpriteRenderer>();
            _scannerLight.sprite = SpriteFactory.Circle(new Color(0.4f, 1f, 0.5f, 0.85f), 14);
            _scannerLight.sortingOrder = SortingLayers.FixtureOverlay;
            _scannerLight.transform.localPosition = new Vector3(rect.WidthCells * 0.5f - 0.3f, 0.15f, 0f);
        }

        public void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 0, 1);
            if (_scannerLight != null)
                _scannerLight.transform.localScale = Level >= 1
                    ? new Vector3(1.5f, 1.5f, 1f)
                    : Vector3.one;
        }

        // ------------------------------------------------------------------
        // 排队 — 设计文档 §9.3「收银台前设置 3 个排队点」
        // ------------------------------------------------------------------
        public Vector2 QueueWorldPosition(int index)
        {
            if (_queuePoints.Count == 0) return cells.CenterWorld;

            if (index < _queuePoints.Count)
                return StoreGrid.CellToWorld(_queuePoints[index]);

            // 队伍超过 3 人时继续向后延伸
            var last = _queuePoints[_queuePoints.Count - 1];
            int overflow = index - _queuePoints.Count + 1;
            return StoreGrid.CellToWorld(new Vector2Int(last.x + overflow, last.y));
        }

        public int Enqueue(CustomerController customer)
        {
            if (!_queue.Contains(customer)) _queue.Add(customer);
            return _queue.IndexOf(customer);
        }

        public void Dequeue(CustomerController customer)
        {
            _queue.Remove(customer);
            if (ActiveCustomer == customer) ActiveCustomer = null;
        }

        public int IndexOf(CustomerController customer) => _queue.IndexOf(customer);

        public CustomerController Head => _queue.Count > 0 ? _queue[0] : null;

        /// <summary>队首顾客是否已经站定、可以开始扫描。</summary>
        public bool HeadReady
        {
            get
            {
                var head = Head;
                return head != null && head.IsWaitingAtCheckout;
            }
        }

        // ------------------------------------------------------------------
        // 交互
        // ------------------------------------------------------------------
        public override bool IsAvailable(PlayerController player)
        {
            if (Game.Manager == null || Game.Manager.State != GameState.Open) return false;
            if (SessionOpen) return false;
            return HeadReady;
        }

        public override string GetPrompt(PlayerController player)
            => $"[E] 开始结账（队伍 {QueueLength} 人）";

        public override void OnInteract(PlayerController player)
        {
            var head = Head;
            if (head == null) return;

            ActiveCustomer = head;

            // 先开界面再触发怪物的 OnCheckout —— 否则检查员弹窗会被收银界面盖住
            Game.UI?.ShowCheckout(this, head);
            head.BeginCheckout();
            Game.Audio?.PlayUiClick();
        }

        /// <summary>收银会话结束（无论成功或顾客离开）。</summary>
        public void CloseSession()
        {
            ActiveCustomer = null;
        }
    }
}
