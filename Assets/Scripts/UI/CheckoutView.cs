using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Customers;
using MonsterMart.Data;
using MonsterMart.Store;

namespace MonsterMart.UI
{
    /// <summary>
    /// 收银界面 — 设计文档 §5。
    /// 商品逐件出现在台面上，玩家把它拖过扫描区域；
    /// 漏扫减少收入、重复扫描降低满意度、扫得太慢顾客掉耐心。
    /// 原型不做真实找零（文档 §5.1「原型简化方案」）。
    /// </summary>
    public class CheckoutView : UIPanel
    {
        /// <summary>收银时仍然要能看到店内，所以不铺全屏遮罩，但仍屏蔽世界输入。</summary>
        public override bool BlocksWorld => true;
        public override bool CanCloseWithEscape => false;

        Text _title;
        Text _statusLabel;
        Text _totalLabel;
        Image _patienceBar;
        RectTransform _counter;
        RectTransform _scanZone;
        Image _scanGlow;
        Button _finishButton;

        Checkout _checkout;
        CustomerController _customer;

        readonly List<ScanItem> _items = new List<ScanItem>();
        float _sessionTime;
        bool _finishing;

        class ScanItem
        {
            public ProductData product;
            public bool scanned;
            public bool swallowed;      // 史莱姆吞下的额外商品
            public DraggableItem widget;
            public Image icon;
            public Text label;
        }

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("CheckoutView", canvas);
            UIFactory.Stretch(Root);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 330), new Vector2(1500, 520));

            _title = UIFactory.Label(window.transform, "收银中", 30, UIFactory.Accent,
                                     TextAnchor.MiddleLeft, "Title");
            UIFactory.Anchor(_title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -40), new Vector2(-60, 38));

            _statusLabel = UIFactory.Label(window.transform, "把商品拖到右侧扫描区", 20, UIFactory.InkDim,
                                           TextAnchor.MiddleLeft, "Status");
            UIFactory.Anchor(_statusLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -76), new Vector2(-60, 28));

            // 顾客耐心条
            var barBack = UIFactory.Panel(window.transform, new Color(0.05f, 0.05f, 0.09f), "PatienceBack");
            UIFactory.Anchor(barBack.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-230, -44), new Vector2(380, 18));

            var fillRt = UIFactory.NewRect("Fill", barBack.transform);
            UIFactory.Stretch(fillRt, 2, 2, 2, 2);
            _patienceBar = fillRt.gameObject.AddComponent<Image>();
            _patienceBar.sprite = UIFactory.White;
            _patienceBar.color = UIFactory.Good;
            _patienceBar.type = Image.Type.Filled;
            _patienceBar.fillMethod = Image.FillMethod.Horizontal;
            _patienceBar.raycastTarget = false;

            // 台面
            var counterPanel = UIFactory.Panel(window.transform, new Color(0.16f, 0.14f, 0.22f), "Counter");
            UIFactory.Anchor(counterPanel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                             new Vector2(520, -30), new Vector2(980, 230));
            _counter = counterPanel.rectTransform;

            // 扫描区
            var zonePanel = UIFactory.Panel(window.transform, new Color(0.18f, 0.30f, 0.22f), "ScanZone");
            UIFactory.Anchor(zonePanel.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                             new Vector2(-190, -30), new Vector2(300, 230));
            _scanZone = zonePanel.rectTransform;

            _scanGlow = UIFactory.Panel(zonePanel.transform, new Color(0.45f, 1f, 0.55f, 0f), "Glow");
            UIFactory.Stretch(_scanGlow.rectTransform);
            _scanGlow.raycastTarget = false;

            var zoneLabel = UIFactory.Label(zonePanel.transform, "扫描区\n把商品拖到这里", 22, UIFactory.Ink,
                                            TextAnchor.MiddleCenter, "ZoneLabel");
            UIFactory.Stretch(zoneLabel.rectTransform);

            _totalLabel = UIFactory.Label(window.transform, "已扫描 0 件 · 合计 0", 24, UIFactory.Warn,
                                          TextAnchor.MiddleLeft, "Total");
            UIFactory.Anchor(_totalLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                             new Vector2(250, 44), new Vector2(440, 34));

            _finishButton = UIFactory.Button(window.transform, "结算", OnFinishPressed, 24,
                                             new Color(0.30f, 0.52f, 0.34f));
            UIFactory.Anchor(_finishButton.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-160, 44), new Vector2(240, 52));
        }

        // ------------------------------------------------------------------
        public void OpenSession(Checkout checkout, CustomerController customer)
        {
            _checkout = checkout;
            _customer = customer;
            _sessionTime = 0f;
            _finishing = false;

            BuildItems();
            base.Open();

            _title.text = $"收银中 · {customer.Data.displayName}";
            _statusLabel.text = customer.WantsDiscreetBag
                ? "顾客好像有话要说……扫完再看看"
                : "把商品拖到右侧扫描区";

            RefreshTotals();
        }

        void BuildItems()
        {
            ClearItems();

            for (int i = 0; i < _customer.Basket.Count; i++)
                AddItem(_customer.Basket[i], false);

            // 史莱姆吞下的额外商品（文档 §4.4）
            for (int i = 0; i < _customer.SwallowedExtra && _customer.Basket.Count > 0; i++)
                AddItem(_customer.Basket[i % _customer.Basket.Count], true);

            LayoutItems();
        }

        void AddItem(ProductData product, bool swallowed)
        {
            var holder = UIFactory.Panel(_counter, new Color(1f, 1f, 1f, 0.06f), "Item");
            holder.rectTransform.sizeDelta = new Vector2(120, 150);

            var icon = UIFactory.Icon(holder.transform, SpriteFactory.ProductIcon(product), 72);
            UIFactory.Anchor(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                             new Vector2(0, -46), new Vector2(72, 72));

            var label = UIFactory.Label(holder.transform,
                swallowed ? $"{product.displayName}\n(吞下的)" : product.displayName,
                16, swallowed ? UIFactory.Warn : UIFactory.Ink, TextAnchor.UpperCenter, "Name");
            UIFactory.Anchor(label.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 26), new Vector2(116, 46));

            var drag = holder.gameObject.AddComponent<DraggableItem>();
            drag.Setup(this, holder.rectTransform);

            var item = new ScanItem
            {
                product = product,
                swallowed = swallowed,
                widget = drag,
                icon = icon,
                label = label,
            };
            drag.item = item;
            _items.Add(item);
        }

        void LayoutItems()
        {
            float spacing = 132f;
            float startX = -(_items.Count - 1) * spacing * 0.5f;

            for (int i = 0; i < _items.Count; i++)
            {
                var rt = _items[i].widget.RectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                _items[i].widget.HomePosition = rt.anchoredPosition;
            }
        }

        void ClearItems()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].widget != null) Destroy(_items[i].widget.gameObject);
            _items.Clear();
        }

        // ------------------------------------------------------------------
        // 拖拽结果判定
        // ------------------------------------------------------------------
        /// <summary>由 DraggableItem 在松手时调用。</summary>
        public void OnItemDropped(ScanItemHandle handle, Vector2 screenPosition)
        {
            var item = handle.item as ScanItem;
            if (item == null || _customer == null) return;

            if (!InsideScanZone(screenPosition))
            {
                _scanGlow.color = new Color(0.45f, 1f, 0.55f, 0f);
                return;
            }

            if (item.scanned)
            {
                // 重复扫描 — 设计文档 §5.1
                _customer.AddSatisfaction(-GameConfig.DoubleScanSatisfactionPenalty);
                _customer.ApplyPatience(-6f);
                Game.Reputation?.Add(GameConfig.RepScanError, "重复扫描");
                Game.Audio?.PlayError();
                _statusLabel.text = "这件已经扫过了！顾客不太高兴。";
                _statusLabel.color = UIFactory.Bad;
                return;
            }

            item.scanned = true;
            item.icon.color = new Color(1f, 1f, 1f, 0.45f);
            item.label.text = item.product.displayName + "\n<color=#7CE07C>已扫描</color>";
            Game.Audio?.PlayScan();

            _scanGlow.color = new Color(0.45f, 1f, 0.55f, 0.35f);

            // 扫到禁忌商品会有特殊对话（文档 §5.1）
            if (item.product.IsDislikedBy(_customer.Data.monsterType))
            {
                _statusLabel.text = $"「你居然把{item.product.displayName}放进我的袋子？」";
                _statusLabel.color = UIFactory.Bad;
                _customer.AddSatisfaction(-10f);
                Game.Reputation?.Add(GameConfig.RepTabooViolation, "把禁忌商品卖给了顾客");
            }
            else
            {
                _statusLabel.text = "扫描成功";
                _statusLabel.color = UIFactory.Good;
            }

            RefreshTotals();
        }

        bool InsideScanZone(Vector2 screenPosition)
        {
            // 判定区域随收银台等级放大（文档 §5.2「扫描判定区域更大」）
            // Canvas 是 ScreenSpaceOverlay，RectTransform.position 就是屏幕像素坐标
            var zoneCenter = _scanZone.position;
            var canvasScale = Root.lossyScale.x <= 0f ? 1f : Root.lossyScale.x;

            float halfW = _scanZone.rect.width * 0.5f * canvasScale * _checkout.ScanWindow;
            float halfH = _scanZone.rect.height * 0.5f * canvasScale * _checkout.ScanWindow;

            return Mathf.Abs(screenPosition.x - zoneCenter.x) <= halfW &&
                   Mathf.Abs(screenPosition.y - zoneCenter.y) <= halfH;
        }

        void RefreshTotals()
        {
            int scanned = 0, total = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].scanned) continue;
                scanned++;
                total += _items[i].product.salePrice;
            }

            _totalLabel.text = $"已扫描 {scanned}/{_items.Count} 件 · 合计 {total}";
        }

        // ------------------------------------------------------------------
        void Update()
        {
            if (!IsOpen || _customer == null) return;

            _sessionTime += Time.deltaTime;

            // 扫描速度太慢 → 顾客耐心下降（文档 §5.1）
            float drain = (1.2f + _sessionTime * 0.08f) * _checkout.QueuePatienceMultiplier;
            _customer.ApplyPatience(-drain * Time.deltaTime);

            // 队伍里其他人也在掉耐心
            var queue = _checkout.Queue;
            for (int i = 1; i < queue.Count; i++)
                queue[i]?.ApplyPatience(-0.4f * Time.deltaTime);

            _patienceBar.fillAmount = _customer.PatienceNormalized;
            _patienceBar.color = _customer.PatienceNormalized > 0.6f ? UIFactory.Good
                               : _customer.PatienceNormalized > 0.3f ? UIFactory.Warn : UIFactory.Bad;

            if (_customer.Patience <= 0f)
            {
                Game.UI.Hud.Flash($"{_customer.Data.displayName} 等不下去了，扔下东西走了");
                AbortSession();
            }

            _scanGlow.color = new Color(0.45f, 1f, 0.55f,
                Mathf.Max(0f, _scanGlow.color.a - Time.deltaTime * 1.2f));
        }

        // ------------------------------------------------------------------
        // 结算
        // ------------------------------------------------------------------
        void OnFinishPressed()
        {
            if (_finishing || _customer == null) return;
            _finishing = true;

            // 特殊请求要先处理完再收钱
            if (_customer.WantsDiscreetBag)
            {
                AskDiscreetBag();
                return;
            }

            if (_customer.SwallowedExtra > 0 && HasUnscannedSwallowed())
            {
                AskSwallowedItems();
                return;
            }

            Finalize(0, 0f);
        }

        bool HasUnscannedSwallowed()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].swallowed && !_items[i].scanned) return true;
            return false;
        }

        /// <summary>吸血鬼的黑袋子请求 — 设计文档 §4.1。</summary>
        void AskDiscreetBag()
        {
            Game.UI.ShowChoice(
                $"{_customer.Data.displayName} 的请求",
                "「请不要把血橙汽水装进透明袋子。」",
                new ChoiceOption("使用黑色袋子", "满意度提升，声望 +4", () =>
                {
                    _customer.WantsDiscreetBag = false;
                    Game.Reputation?.Add(GameConfig.RepPerfectSpecialRequest, "满足了吸血鬼的特殊要求");
                    ContinueFinish(0, 15f);
                }),
                new ChoiceOption("使用普通袋子", "正常结账，不加不减", () =>
                {
                    _customer.WantsDiscreetBag = false;
                    ContinueFinish(0, 0f);
                }),
                new ChoiceOption("拒绝提供特殊服务", "耐心下降，满意度大跌", () =>
                {
                    _customer.WantsDiscreetBag = false;
                    _customer.ApplyPatience(-20f);
                    ContinueFinish(0, -25f);
                }));
        }

        /// <summary>史莱姆吞商品 — 设计文档 §4.4。</summary>
        void AskSwallowedItems()
        {
            int extra = 0;
            int extraValue = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].swallowed || _items[i].scanned) continue;
                extra++;
                extraValue += _items[i].product.salePrice;
            }

            Game.UI.ShowChoice(
                $"{_customer.Data.displayName} 吞下了商品",
                $"它一口气吞下了额外 {extra} 件商品（价值 {extraValue}）。你要怎么处理？",
                new ChoiceOption("照实收取全部费用", $"收入 +{extraValue}，满意度略降", () =>
                {
                    _customer.SwallowedExtra = 0;
                    ContinueFinish(extraValue, -8f);
                }),
                new ChoiceOption("只收一件的钱", "少收一点，换来好感", () =>
                {
                    _customer.SwallowedExtra = 0;
                    int half = extra > 0 ? extraValue / extra : 0;
                    Game.Reputation?.Add(GameConfig.RepPerfectSpecialRequest, "对史莱姆网开一面");
                    ContinueFinish(half, 18f);
                }),
                new ChoiceOption("要求它吐出来", "可能引发污渍爆发", () =>
                {
                    _customer.SwallowedExtra = 0;
                    SpillStains();
                    ContinueFinish(0, -20f);
                }));
        }

        void SpillStains()
        {
            var store = Game.Store;
            if (store == null || _customer == null) return;

            for (int i = 0; i < 4; i++)
            {
                var cell = new Vector2Int(
                    _customer.Cell.x + Random.Range(-2, 3),
                    _customer.Cell.y + Random.Range(-2, 3));
                store.AddStain(cell, _customer.Data.bodyColor);
            }

            Game.UI.Hud.Flash("污渍爆发了！");
            Game.Audio?.PlayAngry();
        }

        void ContinueFinish(int bonusRevenue, float satisfactionDelta)
        {
            // 选择弹窗关闭后可能还有第二个特殊请求
            if (_customer != null && _customer.SwallowedExtra > 0 && HasUnscannedSwallowed())
            {
                AskSwallowedItems();
                return;
            }

            Finalize(bonusRevenue, satisfactionDelta);
        }

        void Finalize(int bonusRevenue, float satisfactionDelta)
        {
            if (_customer == null)
            {
                Close();
                return;
            }

            int revenue = bonusRevenue;
            int missed = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].swallowed) continue;   // 吞下的部分已在弹窗里结算

                if (_items[i].scanned) revenue += _items[i].product.salePrice;
                else missed++;
            }

            float satisfaction = satisfactionDelta;

            if (missed > 0)
            {
                // 漏扫：减少收入（商品白送）+ 满意度惩罚
                satisfaction -= missed * GameConfig.MissedScanSatisfactionPenalty;
                Game.Reputation?.Add(GameConfig.RepScanError * missed, $"漏扫 {missed} 件商品");
                Game.UI.Hud.Flash($"漏扫 {missed} 件，损失了这部分收入");
            }
            else if (_sessionTime < 6f)
            {
                satisfaction += 10f;   // 快速结账（文档 §6.2）
            }

            var customer = _customer;
            _checkout.CloseSession();
            _customer = null;

            Close();
            customer.CompleteCheckout(revenue, satisfaction);
        }

        void AbortSession()
        {
            var customer = _customer;
            _checkout?.CloseSession();
            _customer = null;
            Close();
            customer?.LeaveAngry("收银太慢");
        }

        public override void Close()
        {
            base.Close();
            ClearItems();
            _finishing = false;

            if (_customer != null)
            {
                _checkout?.CloseSession();
                _customer = null;
            }
        }
    }

    /// <summary>让 DraggableItem 能把任意 payload 传回来的最小接口。</summary>
    public class ScanItemHandle
    {
        public object item;
    }

    /// <summary>收银台上可拖动的商品。</summary>
    public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform RectTransform { get; private set; }
        public Vector2 HomePosition { get; set; }

        internal object item;

        CheckoutView _owner;
        readonly ScanItemHandle _handle = new ScanItemHandle();

        public void Setup(CheckoutView owner, RectTransform rt)
        {
            _owner = owner;
            RectTransform = rt;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            RectTransform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData e)
        {
            float scale = RectTransform.lossyScale.x <= 0f ? 1f : RectTransform.lossyScale.x;
            RectTransform.anchoredPosition += e.delta / scale;
        }

        public void OnEndDrag(PointerEventData e)
        {
            _handle.item = item;
            _owner?.OnItemDropped(_handle, e.position);
            RectTransform.anchoredPosition = HomePosition;
        }
    }
}
