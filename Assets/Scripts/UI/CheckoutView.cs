using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Customers;
using MonsterMart.Data;
using MonsterMart.Store;

namespace MonsterMart.UI
{
    /// <summary>
    /// 收银界面 — 设计文档 §5。
    /// 商品逐件出现在台面上，玩家点一下就扫描；
    /// 漏扫减少收入、重复扫描降低满意度、扫得太慢顾客掉耐心。
    /// 原型不做真实找零（文档 §5.1「原型简化方案」）。
    ///
    /// 用户反馈明确要求「直接点击一件结账，不需要一个个拖到扫描区域」——
    /// 以前是把商品拖进一个判定区（位置技巧），现在改成点击直接扫描，
    /// 收银台升级 / 收银岗位的加成从「判定区更大」改成「两次扫描间隔更短」
    /// （见 Checkout.ScanIntervalSeconds），经济系统还是有意义的，只是不再考验手速。
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
        Button _finishButton;

        Checkout _checkout;
        CustomerController _customer;

        readonly List<ScanItem> _items = new List<ScanItem>();
        float _sessionTime;
        float _scanLockRemaining;
        bool _finishing;

        class ScanItem
        {
            public ProductData product;
            public bool scanned;
            public bool swallowed;      // 史莱姆吞下的额外商品
            public Image panel;
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

            _statusLabel = UIFactory.Label(window.transform, "点一下商品完成扫描", 20, UIFactory.InkDim,
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

            // 台面：以前一半空间给拖拽用的扫描区，现在点击直接扫描，台面吃满整行
            var counterPanel = UIFactory.Panel(window.transform, new Color(0.16f, 0.14f, 0.22f), "Counter");
            UIFactory.Anchor(counterPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0, -30), new Vector2(1360, 230));
            _counter = counterPanel.rectTransform;

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
                : "点一下商品完成扫描";

            _scanLockRemaining = 0f;
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

            var item = new ScanItem
            {
                product = product,
                swallowed = swallowed,
                panel = holder,
                icon = icon,
                label = label,
            };

            var button = holder.gameObject.AddComponent<Button>();
            button.targetGraphic = holder;
            button.onClick.AddListener(() => OnItemClicked(item));

            _items.Add(item);
        }

        void LayoutItems()
        {
            float spacing = 132f;
            float startX = -(_items.Count - 1) * spacing * 0.5f;

            for (int i = 0; i < _items.Count; i++)
            {
                var rt = _items[i].panel.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
            }
        }

        void ClearItems()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].panel != null) Destroy(_items[i].panel.gameObject);
            _items.Clear();
        }

        // ------------------------------------------------------------------
        // 测试专用只读入口——不用真的构造 Button 点击事件（EditMode 下也没有
        // EventSystem 处理指针事件），直接调用和真实点击同一条逻辑。
        // ------------------------------------------------------------------
        public int ItemCount => _items.Count;
        public bool IsItemScanned(int index) => index >= 0 && index < _items.Count && _items[index].scanned;
        public void ClickItem(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            OnItemClicked(_items[index]);
        }

        /// <summary>推进扫描间隔计时，从 Update 拆出来是为了让用例能直接推进，不用真的等一帧。</summary>
        public void TickScanLock(float dt)
        {
            if (_scanLockRemaining > 0f) _scanLockRemaining -= dt;
        }

        // ------------------------------------------------------------------
        // 扫描判定 —— 点一下就扫，不用拖到指定区域
        // ------------------------------------------------------------------
        void OnItemClicked(ScanItem item)
        {
            if (item == null || _customer == null) return;

            if (_scanLockRemaining > 0f)
            {
                // 收银台升级 / 收银岗位就体现在这个间隔上——见 Checkout.ScanIntervalSeconds
                Game.Audio?.PlayError();
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

            // 收银台升级 / 收银岗位就体现在这个间隔上——不再是判定区大小
            _scanLockRemaining = _checkout != null ? _checkout.ScanIntervalSeconds : 0f;

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
        /// <summary>收银会话只在营业中推进。</summary>
        static bool SessionTicks =>
            Game.Manager != null && Game.Manager.State == GameState.Open;

        /// <summary>
        /// 推进一帧收银会话的模拟部分（累计耗时 + 扣当前顾客和队列的耐心），
        /// 不碰任何 UI 控件；返回累计后的会话时长，没推进时原样返回。
        ///
        /// 会看游戏状态，是因为 CheckoutView 的 Esc 关不掉
        /// （CanCloseWithEscape = false），暂停时它会留在屏幕上。不看状态的话，
        /// 玩家人在暂停菜单里，收银耗时照样累计、当前顾客和整条队伍照样掉耐心，
        /// 掉到 0 还会愤怒离店 —— 声望 -6、LeftAngry +1 全在暂停期间发生，
        /// 而 LeftAngry 正是第三天检查员「服务事故」那一项的输入。
        ///
        /// 抽成静态、只吃参数，是为了能无头验证这条闸门（Update 里那些控件
        /// 没建过 UI 就会空引用）。
        /// </summary>
        public static float AdvanceSession(Checkout checkout, CustomerController customer,
                                           float sessionTime, float dt)
        {
            if (checkout == null || customer == null) return sessionTime;
            if (!SessionTicks) return sessionTime;

            sessionTime += dt;

            // 扫描速度太慢 → 顾客耐心下降（文档 §5.1）
            float drain = (1.2f + sessionTime * 0.08f) * checkout.QueuePatienceMultiplier;
            customer.ApplyPatience(-drain * dt);

            // 队伍里其他人也在掉耐心
            var queue = checkout.Queue;
            for (int i = 1; i < queue.Count; i++)
                queue[i]?.ApplyPatience(-0.4f * dt);

            return sessionTime;
        }

        void Update()
        {
            if (!IsOpen || _customer == null) return;
            if (!SessionTicks) return;   // 暂停 / 结算时整个收银界面冻住

            _sessionTime = AdvanceSession(_checkout, _customer, _sessionTime, Time.deltaTime);

            _patienceBar.fillAmount = _customer.PatienceNormalized;
            _patienceBar.color = _customer.PatienceNormalized > 0.6f ? UIFactory.Good
                               : _customer.PatienceNormalized > 0.3f ? UIFactory.Warn : UIFactory.Bad;

            if (_customer.Patience <= 0f)
            {
                Game.UI.Hud.Flash($"{_customer.Data.displayName} 等不下去了，扔下东西走了");
                AbortSession();
            }

            TickScanLock(Time.deltaTime);
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

            // 只要商品离开了货架就产生成本 —— 漏扫等于把货白送出去，
            // 所以漏扫的那份成本照样计入，收入却是 0。
            int costOfGoods = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                costOfGoods += _items[i].product.purchasePrice;

                if (_items[i].swallowed) continue;   // 吞下的部分已在弹窗里结算收入

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
            customer.CompleteCheckout(revenue, costOfGoods, satisfaction);
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
}
