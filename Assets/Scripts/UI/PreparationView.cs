using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.UI
{
    /// <summary>
    /// 进货界面 — 设计文档 §10.2 与 §2.1 阶段一。
    ///
    /// 用户反馈明确要求「即便开始营业了也可以打开商店进行补货，随时可以补货」——
    /// 以前这个面板只在闭店准备阶段能打开，营业开始后 B 键就失效了。现在营业中
    /// 也能随时调出来临时加购，靠 <see cref="DuringBusiness"/> 区分两种场景：
    /// 「开始营业」按钮只在营业前有意义，营业中打开就该隐藏掉，防止误触发
    /// 第二次 BeginBusiness（那会把营业计时器和随机事件重新初始化一遍）。
    /// </summary>
    public class PreparationView : UIPanel
    {
        // 营业前不许 Esc 关（那时候没有时间压力，防止手滑关掉又要重开一次）；
        // 营业中打开的是「随时补货」这个副屏操作，Esc 该能随手关掉。
        public override bool CanCloseWithEscape => DuringBusiness;

        /// <summary>这次是不是在营业中打开的（而不是闭店准备阶段）。</summary>
        public bool DuringBusiness => Game.Manager != null && Game.Manager.State == GameState.Open;

        /// <summary>「开始营业」按钮当前是否显示 —— 给回归用例验证营业中它有没有让位。</summary>
        public bool StartButtonVisible => _startButton != null && _startButton.gameObject.activeSelf;

        Text _title;
        Text _briefing;
        Text _goalLabel;
        Text _moneyLabel;
        Transform _productList;
        Transform _shelfPreview;
        Transform _crowdList;
        Text _crowdSummary;
        Button _upgradeButton;
        Text _upgradeLabel;
        Button _autoStockButton;
        Text _autoStockLabel;
        Text _expeditionStatus;

        Button _startButton;
        Button _closeButton;
        Text _closeLabel;

        readonly List<ProductRow> _rows = new List<ProductRow>();
        readonly List<Text> _previewRows = new List<Text>();
        readonly List<Text> _crowdRows = new List<Text>();

        class ProductRow
        {
            public ProductData product;
            public Text stockLabel;
            public Button buyOne;
            public Button buyFive;
        }

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("PreparationView", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, UIFactory.Scrim, "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(1500, 900));

            BuildHeader(window.transform);
            BuildLeftColumn(window.transform);
            BuildRightColumn(window.transform);
            BuildFooter(window.transform);
        }

        // 版式常量集中在这里，改一处就能整体让位，避免文字互相重叠
        const float HeaderHeight = 250f;   // 标题 + 简报 + 目标占用的高度
        const float BoxHeight = 500f;      // 左右两栏的高度
        const float RowHeight = 46f;       // 商品行高
        const float RowSpacing = 3f;

        void BuildHeader(Transform window)
        {
            _title = UIFactory.Label(window, "第 1 天 · 营业前准备", 38, UIFactory.Accent,
                                     TextAnchor.MiddleLeft, "Title");
            UIFactory.Anchor(_title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -46), new Vector2(-80, 48));

            _briefing = UIFactory.Label(window, "", 19, UIFactory.InkDim, TextAnchor.UpperLeft, "Briefing");
            _briefing.lineSpacing = 1.15f;
            UIFactory.Anchor(_briefing.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -140), new Vector2(-80, 124));

            _goalLabel = UIFactory.Label(window, "", 19, UIFactory.Warn, TextAnchor.MiddleLeft, "Goal");
            UIFactory.Anchor(_goalLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -222), new Vector2(-80, 34));
        }

        /// <summary>左右两栏共用的垂直位置：正好压在 HeaderHeight 下面。</summary>
        static float ColumnCenterY => 450f - HeaderHeight - 10f - BoxHeight * 0.5f;

        void BuildLeftColumn(Transform window)
        {
            var box = UIFactory.Panel(window, UIFactory.PanelBgSoft, "ProductBox");
            UIFactory.Anchor(box.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                             new Vector2(430, ColumnCenterY), new Vector2(820, BoxHeight));

            var header = UIFactory.Label(box.transform, "进货 · 商品列表", 24, UIFactory.Accent,
                                         TextAnchor.MiddleLeft, "Header");
            UIFactory.Anchor(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -26), new Vector2(-32, 32));

            // 资金放在这一行的右端，把底部整行让给按钮
            _moneyLabel = UIFactory.Label(box.transform, "", 21, UIFactory.Warn,
                                          TextAnchor.MiddleRight, "Money");
            UIFactory.Anchor(_moneyLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -26), new Vector2(-32, 32));

            var listRt = UIFactory.NewRect("List", box.transform);
            UIFactory.Stretch(listRt, 16, 16, 16, 52);

            var group = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = RowSpacing;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;

            _productList = listRt;
            BuildProductRows();
        }

        void BuildProductRows()
        {
            _rows.Clear();

            for (int i = 0; i < GameDatabase.Products.Count; i++)
            {
                var product = GameDatabase.Products[i];

                var rowPanel = UIFactory.Panel(_productList, new Color(1f, 1f, 1f, 0.04f), "Row");
                UIFactory.Size(rowPanel.gameObject, -1, RowHeight, -1, RowHeight);

                var hGroup = rowPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
                hGroup.spacing = 10;
                hGroup.padding = new RectOffset(10, 10, 4, 4);
                hGroup.childAlignment = TextAnchor.MiddleLeft;
                hGroup.childForceExpandWidth = false;
                hGroup.childForceExpandHeight = true;
                hGroup.childControlWidth = true;
                hGroup.childControlHeight = true;

                UIFactory.Icon(rowPanel.transform, SpriteFactory.ProductIcon(product), 32);

                var name = UIFactory.Label(rowPanel.transform, product.displayName, 19, UIFactory.Ink,
                                           TextAnchor.MiddleLeft, "Name");
                UIFactory.Size(name.gameObject, 140, -1, 140, -1);

                var price = UIFactory.Label(rowPanel.transform,
                    $"进 {product.purchasePrice} / 售 {product.salePrice}", 17, UIFactory.InkDim,
                    TextAnchor.MiddleLeft, "Price");
                UIFactory.Size(price.gameObject, 130, -1, 130, -1);

                var tag = UIFactory.Label(rowPanel.transform, PreferenceTag(product), 16,
                                          TagColor(product), TextAnchor.MiddleLeft, "Tag");
                UIFactory.Size(tag.gameObject, 165, -1, 165, -1);

                var stock = UIFactory.Label(rowPanel.transform, "仓库 0", 18, UIFactory.Warn,
                                            TextAnchor.MiddleCenter, "Stock");
                UIFactory.Size(stock.gameObject, 90, -1, 90, -1);

                var captured = product;
                var buy1 = UIFactory.Button(rowPanel.transform, "+1", () => Buy(captured, 1), 18);
                UIFactory.Size(buy1.gameObject, 54, 32, 54, 32);

                var buy5 = UIFactory.Button(rowPanel.transform, "+5", () => Buy(captured, 5), 18);
                UIFactory.Size(buy5.gameObject, 54, 32, 54, 32);

                _rows.Add(new ProductRow
                {
                    product = product,
                    stockLabel = stock,
                    buyOne = buy1,
                    buyFive = buy5,
                });
            }
        }

        static string PreferenceTag(ProductData p)
        {
            if (p.isCleaningTool) return "可清理污渍";
            if (p.hasPreference)
            {
                var data = GameDatabase.GetCustomer(p.preferredBy);
                return data != null ? $"{data.displayName} 喜欢" : "";
            }
            if (p.hasDislike)
            {
                var data = GameDatabase.GetCustomer(p.dislikedBy);
                return data != null ? $"{data.displayName} 讨厌" : "";
            }
            return "";
        }

        static Color TagColor(ProductData p)
        {
            if (p.isCleaningTool) return new Color(0.45f, 0.78f, 0.95f);
            if (p.hasPreference) return UIFactory.Good;
            if (p.hasDislike) return UIFactory.Bad;
            return UIFactory.InkDim;
        }

        void BuildRightColumn(Transform window)
        {
            var box = UIFactory.Panel(window, UIFactory.PanelBgSoft, "PreviewBox");
            UIFactory.Anchor(box.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                             new Vector2(-330, ColumnCenterY), new Vector2(600, BoxHeight));

            // ---- 今晚的预约条（设计文档 §2.1 的推理版实现）----
            var crowdHeader = UIFactory.Label(box.transform, "今晚的预约条", 23, UIFactory.Accent,
                                              TextAnchor.MiddleLeft, "CrowdHeader");
            UIFactory.Anchor(crowdHeader.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -26), new Vector2(-32, 30));

            var crowdRt = UIFactory.NewRect("CrowdList", box.transform);
            UIFactory.Stretch(crowdRt, 16, BoxHeight - 212f, 16, 46);

            var crowdGroup = crowdRt.gameObject.AddComponent<VerticalLayoutGroup>();
            crowdGroup.spacing = 2;
            crowdGroup.childAlignment = TextAnchor.UpperLeft;
            crowdGroup.childForceExpandWidth = true;
            crowdGroup.childForceExpandHeight = false;
            crowdGroup.childControlWidth = true;
            crowdGroup.childControlHeight = true;
            _crowdList = crowdRt;

            _crowdSummary = UIFactory.Label(box.transform, "", 16, UIFactory.Warn,
                                            TextAnchor.MiddleLeft, "CrowdSummary");
            UIFactory.Anchor(_crowdSummary.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -228), new Vector2(-32, 26));

            // ---- 货架预览 ----
            var header = UIFactory.Label(box.transform, "货架预览 · 预计库存", 23, UIFactory.Accent,
                                         TextAnchor.MiddleLeft, "Header");
            UIFactory.Anchor(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -258), new Vector2(-32, 28));

            var listRt = UIFactory.NewRect("List", box.transform);
            UIFactory.Stretch(listRt, 16, 16, 16, 276);

            var group = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 2;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;

            _shelfPreview = listRt;
        }

        /// <summary>
        /// 展示今晚的预约条。只给线索、不点名 —— 玩家要自己翻译成进货清单。
        /// 对照表在怪物图鉴里（Tab）。
        /// </summary>
        void RefreshCrowd(DayPlan plan)
        {
            var notes = Game.Day != null ? Game.Day.Notes : null;
            int noteCount = notes != null ? notes.Count : 0;

            while (_crowdRows.Count < noteCount)
            {
                var label = UIFactory.Label(_crowdList, "", 16, UIFactory.Ink,
                                            TextAnchor.MiddleLeft, "CrowdRow");
                UIFactory.Size(label.gameObject, -1, 30, -1, 30);
                _crowdRows.Add(label);
            }

            for (int i = 0; i < _crowdRows.Count; i++)
            {
                if (i >= noteCount)
                {
                    _crowdRows[i].text = "";
                    continue;
                }

                var note = notes[i];
                string tail = note.count > 1 ? $"　<color=#FFD966>×{note.count}</color>" : "";
                _crowdRows[i].text = $"<color=#8FA8C8>·</color> 「{note.text}」{tail}";
            }

            int total = plan != null ? plan.TotalCustomers : 0;
            int perHead = plan != null ? plan.maxItemsPerCustomer : 0;
            _crowdSummary.text =
                $"共 {total} 位客人，每人最多买 {perHead} 件。看不懂线索就按 Tab 查图鉴。";
        }

        void BuildFooter(Transform window)
        {
            _startButton = UIFactory.Button(window, "开始营业", TryBeginBusiness,
                                            25, new Color(0.30f, 0.52f, 0.34f));
            UIFactory.Anchor(_startButton.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-150, 62), new Vector2(220, 56));

            // 一键摆货：准备阶段没有时间压力，来回搬运只是重复劳动
            _autoStockButton = UIFactory.Button(window, "一键摆货", AutoStock, 22,
                                                new Color(0.32f, 0.44f, 0.30f));
            UIFactory.Anchor(_autoStockButton.GetComponent<RectTransform>(),
                             new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-400, 62), new Vector2(250, 56));
            _autoStockLabel = _autoStockButton.GetComponentInChildren<Text>();

            _closeButton = UIFactory.Button(window, "自己去店里摆", Close, 21,
                                            new Color(0.28f, 0.38f, 0.55f));
            UIFactory.Anchor(_closeButton.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-670, 62), new Vector2(250, 56));
            _closeLabel = _closeButton.GetComponentInChildren<Text>();

            var bestiary = UIFactory.Button(window, "图鉴 (Tab)", () =>
            {
                Game.UI.ToggleBestiary();
            }, 20);
            UIFactory.Anchor(bestiary.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-905, 62), new Vector2(180, 56));

            // 远征已经挪到晨会（§2.1 阶段一）——一天只有一趟，
            // 这里只留一行状态，免得玩家以为还能再去一次。
            _expeditionStatus = UIFactory.Label(window, "", 18, UIFactory.InkDim,
                                                TextAnchor.MiddleLeft, "ExpeditionStatus");
            UIFactory.Anchor(_expeditionStatus.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                             new Vector2(300, 62), new Vector2(460, 56));

            _upgradeButton = UIFactory.Button(window, "", UpgradeCheckout, 20);
            UIFactory.Anchor(_upgradeButton.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-1165, 62), new Vector2(300, 56));
            _upgradeLabel = _upgradeButton.GetComponentInChildren<Text>();
        }

        // ------------------------------------------------------------------
        public void OpenFor(DayPlan plan)
        {
            base.Open();

            if (plan != null)
            {
                // plan.title 只是主题词（不带「第 N 天」）——天数循环复用同一套
                // DayPlan 之后，前缀得从当前真实天数现拼，不能烧在数据里
                _title.text = $"第 {Game.Day.CurrentDay} 天 · {plan.title}";
                _briefing.text = plan.briefing;
                _goalLabel.text = "今晚目标：" + plan.goalDescription;
            }

            RefreshFooterForContext();
            RefreshCrowd(plan);
            RefreshAll();
        }

        /// <summary>
        /// 营业中打开的话，「开始营业」按钮没有意义（店已经开了），
        /// 关闭按钮的文案也该从「回去摆货」换成「继续营业」。
        /// </summary>
        void RefreshFooterForContext()
        {
            bool duringBusiness = DuringBusiness;

            _startButton.gameObject.SetActive(!duringBusiness);
            _closeLabel.text = duringBusiness ? "继续营业" : "自己去店里摆";

            if (duringBusiness) _title.text = "补货";
        }

        /// <summary>
        /// 仓库空着就开门 = 这一天必然失败（没货可补、没东西可卖）。
        /// 新玩家很容易直接点「开始营业」，所以在这里拦一道。
        /// </summary>
        public void TryBeginBusiness()
        {
            // 双重保险：正常情况下按钮在营业中会被隐藏点不到，但防止意外触发
            // 重复调用 BeginBusiness —— 那会把营业计时器和随机事件重新初始化一遍。
            if (DuringBusiness) return;

            int totalStock = 0;
            for (int i = 0; i < GameDatabase.Products.Count; i++)
                totalStock += Game.Store.WarehouseCount(GameDatabase.Products[i]);

            bool shelvesEmpty = Game.Store.EmptyShelfCount() >= Game.Store.SalesShelfCount();

            if (totalStock <= 0 && shelvesEmpty)
            {
                Game.Audio?.PlayError();
                Game.UI.ShowChoice(
                    "仓库是空的",
                    "你还没有进任何货。这样开门的话，货架永远补不满，顾客买不到东西，\n" +
                    "这一天的目标不可能完成。\n\n建议先在左侧商品列表点 +1 / +5 买一些。",
                    new ChoiceOption("回去进货", "推荐", () => { }),
                    new ChoiceOption("我知道，照样开门", "空手营业", () => Game.Manager.BeginBusiness()));
                return;
            }

            Game.Manager.BeginBusiness();
        }

        /// <summary>
        /// 今天那趟远征的结果摘要 —— 出发按钮在晨会界面上（§2.1 阶段一）。
        /// </summary>
        string ExpeditionStatusText()
        {
            var squad = StaffRoster.ExpeditionSquad();

            if (Game.Manager != null && !Game.Manager.ExpeditionDoneToday)
                return "<color=#FFD966>今天还没出门</color>";

            if (squad.Length == 0)
                return "今天没有出门进货，只能卖现有库存";

            var names = new List<string>();
            for (int i = 0; i < squad.Length; i++)
            {
                var data = GameDatabase.GetStaff(squad[i]);
                if (data != null) names.Add(data.displayName);
            }
            return $"今天出征：{string.Join("、", names)}　" +
                   "<color=#8FA8C8>（他们晚上会累）</color>";
        }

        void Buy(ProductData product, int amount)
        {
            int cost = product.purchasePrice * amount;

            if (!Game.Economy.TrySpend(cost, true))
            {
                Game.UI.Hud.Flash("钱不够了");
                Game.Audio?.PlayError();
                return;
            }

            Game.Store.AddToWarehouse(product, amount);
            Game.Audio?.PlayUiClick();
            RefreshAll();
        }

        /// <summary>把仓库里的货一次性铺到对应货架上。</summary>
        public void AutoStock()
        {
            int placed = Game.Store.AutoRestockAll();

            if (placed <= 0)
            {
                Game.UI.Hud.Flash("没有可以上架的货了（仓库空了，或货架已满）");
                Game.Audio?.PlayError();
            }
            else
            {
                Game.UI.Hud.Flash($"已上架 {placed} 件");
                Game.Audio?.PlayRestock();
            }

            RefreshAll();
        }

        void UpgradeCheckout()
        {
            var checkout = Game.Store.Checkout;
            if (checkout.Level >= 1) return;

            if (!Game.Economy.TrySpend(GameConfig.CheckoutUpgradeCost, false))
            {
                Game.UI.Hud.Flash("钱不够升级收银台");
                Game.Audio?.PlayError();
                return;
            }

            checkout.SetLevel(1);
            Game.UI.Hud.Flash("收银台已升级：扫描更快，排队掉耐心更慢");
            Game.Audio?.PlayUiClick();
            RefreshAll();
        }

        void RefreshAll()
        {
            int totalStock = 0;
            for (int i = 0; i < GameDatabase.Products.Count; i++)
                totalStock += Game.Store.WarehouseCount(GameDatabase.Products[i]);

            _moneyLabel.text = totalStock > 0
                ? $"资金 {Game.Economy.Money}　·　仓库 {totalStock} 件"
                : $"资金 {Game.Economy.Money}　·　<color=#F26B61>仓库是空的</color>";

            if (_autoStockLabel != null)
            {
                _autoStockLabel.text = totalStock > 0 ? $"一键摆货（{totalStock} 件）" : "一键摆货";
                _autoStockButton.interactable = totalStock > 0;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                int inWarehouse = Game.Store.WarehouseCount(row.product);
                row.stockLabel.text = $"仓库 {inWarehouse}";

                bool canBuy1 = Game.Economy.CanAfford(row.product.purchasePrice);
                bool canBuy5 = Game.Economy.CanAfford(row.product.purchasePrice * 5);
                row.buyOne.interactable = canBuy1;
                row.buyFive.interactable = canBuy5;
            }

            if (_expeditionStatus != null)
                _expeditionStatus.text = ExpeditionStatusText();

            RefreshPreview();
            RefreshUpgradeButton();
        }

        void RefreshPreview()
        {
            var shelves = Game.Store.Shelves;

            while (_previewRows.Count < shelves.Count)
            {
                var label = UIFactory.Label(_shelfPreview, "", 15, UIFactory.Ink,
                                            TextAnchor.MiddleLeft, "PreviewRow");
                UIFactory.Size(label.gameObject, -1, 24, -1, 24);
                _previewRows.Add(label);
            }

            for (int i = 0; i < _previewRows.Count; i++)
            {
                if (i >= shelves.Count)
                {
                    _previewRows[i].text = "";
                    continue;
                }

                var shelf = shelves[i];
                int warehouse = Game.Store.WarehouseCount(shelf.product);
                int projected = Mathf.Min(shelf.capacity, shelf.count + warehouse);

                if (shelf.isSupplyRack)
                {
                    // 清洁用品架不卖货，缺了也不算事故
                    _previewRows[i].text =
                        $"{shelf.displayName}　{shelf.product.displayName}　架上 {shelf.count}　→ 可补到 {projected}　<color=#8FA8C8>（自用）</color>";
                    _previewRows[i].color = UIFactory.InkDim;
                    continue;
                }

                _previewRows[i].text =
                    $"{shelf.displayName}　{shelf.product.displayName}　{shelf.count}/{shelf.capacity}　→ 可补到 {projected}";

                _previewRows[i].color = projected <= 0 ? UIFactory.Bad
                                      : projected < shelf.capacity / 2 ? UIFactory.Warn
                                      : UIFactory.Ink;
            }
        }

        void RefreshUpgradeButton()
        {
            var checkout = Game.Store.Checkout;

            if (checkout.Level >= 1)
            {
                _upgradeLabel.text = "收银台已升级";
                _upgradeButton.interactable = false;
                return;
            }

            _upgradeLabel.text = $"升级收银台（{GameConfig.CheckoutUpgradeCost}）";
            _upgradeButton.interactable = Game.Economy.CanAfford(GameConfig.CheckoutUpgradeCost);
        }
    }

    /// <summary>仓库取货面板 — 玩家靠近仓库按 E 时弹出。</summary>
    public class StockRoomView : UIPanel
    {
        Transform _list;
        readonly List<Row> _rows = new List<Row>();
        Button _putBackButton;

        class Row
        {
            public ProductData product;
            public Button button;
            public Text label;
        }

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("StockRoomView", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, UIFactory.Scrim, "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(760, 640));

            var title = UIFactory.Label(window.transform, "仓库 · 选择要携带的商品", 30, UIFactory.Accent,
                                        TextAnchor.MiddleLeft, "Title");
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -44), new Vector2(-60, 40));

            var hint = UIFactory.Label(window.transform,
                $"一次只能带一种商品，最多 {GameConfig.PlayerCarryCapacity} 件（换商品会把手上的退回仓库）",
                18, UIFactory.InkDim, TextAnchor.MiddleLeft, "Hint");
            UIFactory.Anchor(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -82), new Vector2(-60, 28));

            var listRt = UIFactory.NewRect("List", window.transform);
            UIFactory.Stretch(listRt, 30, 90, 30, 110);

            var group = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 6;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;
            _list = listRt;

            var close = UIFactory.Button(window.transform, "关闭 (Esc)", Close, 20);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(130, 46), new Vector2(220, 50));

            // 用户反馈明确要求「应该能把手上的东西放回仓库，而不是只能切换商品」——
            // 以前唯一「腾空手」的办法是点一个有库存的其他商品，把手上的顺带换掉，
            // 货架满了没处卸、又不想随手拿件不需要的东西时会被卡住。
            _putBackButton = UIFactory.Button(window.transform, "放回仓库", PutBackCarry, 20,
                                              new Color(0.42f, 0.30f, 0.22f));
            UIFactory.Anchor(_putBackButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(-130, 46), new Vector2(220, 50));

            BuildRows();
        }

        void BuildRows()
        {
            _rows.Clear();

            for (int i = 0; i < GameDatabase.Products.Count; i++)
            {
                var product = GameDatabase.Products[i];
                var captured = product;

                var button = UIFactory.Button(_list, "", () => Pick(captured), 20);
                UIFactory.Size(button.gameObject, -1, 52, -1, 52);

                var caption = button.GetComponentInChildren<Text>();
                caption.alignment = TextAnchor.MiddleLeft;

                _rows.Add(new Row { product = product, button = button, label = caption });
            }
        }

        public void OpenPicker()
        {
            base.Open();
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                int available = Game.Store.WarehouseCount(row.product);

                row.label.text = $"    {row.product.displayName}　仓库 {available} 件";
                row.button.interactable = available > 0;
                row.label.color = available > 0 ? UIFactory.Ink : UIFactory.InkDim;
            }

            var carry = Game.Player != null ? Game.Player.Carry : null;
            bool holding = carry != null && !carry.IsEmpty;
            _putBackButton.interactable = holding;
        }

        void Pick(ProductData product)
        {
            int taken = Game.Player.TakeFromWarehouse(product);

            if (taken <= 0)
            {
                Game.UI.Hud.Flash("仓库里没有这件商品了");
                Game.Audio?.PlayError();
                Refresh();
                return;
            }

            Game.UI.Hud.Flash($"拿起 {product.displayName} ×{taken}");
            Close();
        }

        /// <summary>
        /// 手上的东西不想要了（比如目标货架满了、又不想顺手拿件别的）——
        /// 直接放回仓库，腾出手来。公开出来给按钮和回归用例共用。
        /// </summary>
        public void PutBackCarry()
        {
            var carry = Game.Player != null ? Game.Player.Carry : null;
            if (carry == null || carry.IsEmpty)
            {
                Game.UI.Hud.Flash("手上没有东西");
                return;
            }

            Game.Store.AddToWarehouse(carry.Product, carry.Count);
            Game.UI.Hud.Flash($"{carry.Product.displayName} ×{carry.Count} 放回仓库");
            carry.Clear();
            Game.Audio?.PlayUiClick();
            Refresh();
        }
    }
}
