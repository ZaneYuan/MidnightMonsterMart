using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.UI
{
    /// <summary>营业前界面 — 设计文档 §10.2 与 §2.1 阶段一。</summary>
    public class PreparationView : UIPanel
    {
        public override bool CanCloseWithEscape => false;

        Text _title;
        Text _briefing;
        Text _goalLabel;
        Text _moneyLabel;
        Transform _productList;
        Transform _shelfPreview;
        Button _upgradeButton;
        Text _upgradeLabel;

        readonly List<ProductRow> _rows = new List<ProductRow>();
        readonly List<Text> _previewRows = new List<Text>();

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

            var header = UIFactory.Label(box.transform, "货架预览 · 预计库存", 24, UIFactory.Accent,
                                         TextAnchor.MiddleLeft, "Header");
            UIFactory.Anchor(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -26), new Vector2(-32, 32));

            var listRt = UIFactory.NewRect("List", box.transform);
            UIFactory.Stretch(listRt, 16, 16, 16, 52);

            var group = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 4;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;

            _shelfPreview = listRt;
        }

        void BuildFooter(Transform window)
        {
            // 两行显示，避免「当前资金 + 空仓库警告」挤成一行撞到右边的按钮
            _moneyLabel = UIFactory.Label(window, "当前资金 0", 21, UIFactory.Warn,
                                          TextAnchor.MiddleLeft, "Money");
            UIFactory.Anchor(_moneyLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                             new Vector2(240, 66), new Vector2(400, 62));

            var start = UIFactory.Button(window, "开始营业", TryBeginBusiness,
                                         26, new Color(0.30f, 0.52f, 0.34f));
            UIFactory.Anchor(start.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-160, 62), new Vector2(240, 56));

            // 关掉面板去店里摆货 —— 准备阶段没有时间限制（设计文档 §2.1）
            var stock = UIFactory.Button(window, "先去店里摆货", Close, 22,
                                         new Color(0.28f, 0.38f, 0.55f));
            UIFactory.Anchor(stock.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-420, 62), new Vector2(240, 56));

            var bestiary = UIFactory.Button(window, "图鉴 (Tab)", () =>
            {
                Game.UI.ToggleBestiary();
            }, 20);
            UIFactory.Anchor(bestiary.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-640, 62), new Vector2(170, 56));

            _upgradeButton = UIFactory.Button(window, "", UpgradeCheckout, 20);
            UIFactory.Anchor(_upgradeButton.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-880, 62), new Vector2(280, 56));
            _upgradeLabel = _upgradeButton.GetComponentInChildren<Text>();
        }

        // ------------------------------------------------------------------
        public void OpenFor(DayPlan plan)
        {
            base.Open();

            if (plan != null)
            {
                _title.text = plan.title;
                _briefing.text = plan.briefing;
                _goalLabel.text = "今晚目标：" + plan.goalDescription;
            }

            RefreshAll();
        }

        /// <summary>
        /// 仓库空着就开门 = 这一天必然失败（没货可补、没东西可卖）。
        /// 新玩家很容易直接点「开始营业」，所以在这里拦一道。
        /// </summary>
        public void TryBeginBusiness()
        {
            int totalStock = 0;
            for (int i = 0; i < GameDatabase.Products.Count; i++)
                totalStock += Game.Store.WarehouseCount(GameDatabase.Products[i]);

            bool shelvesEmpty = Game.Store.EmptyShelfCount() >= Game.Store.Shelves.Count;

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
            Game.UI.Hud.Flash("收银台已升级：扫描判定更大，排队掉耐心更慢");
            Game.Audio?.PlayUiClick();
            RefreshAll();
        }

        void RefreshAll()
        {
            int totalStock = 0;
            for (int i = 0; i < GameDatabase.Products.Count; i++)
                totalStock += Game.Store.WarehouseCount(GameDatabase.Products[i]);

            _moneyLabel.text = totalStock > 0
                ? $"当前资金 {Game.Economy.Money}\n<size=18>仓库共 {totalStock} 件</size>"
                : $"当前资金 {Game.Economy.Money}\n<size=18><color=#F26B61>仓库是空的，先买点货</color></size>";

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

            RefreshPreview();
            RefreshUpgradeButton();
        }

        void RefreshPreview()
        {
            var shelves = Game.Store.Shelves;

            while (_previewRows.Count < shelves.Count)
            {
                var label = UIFactory.Label(_shelfPreview, "", 19, UIFactory.Ink,
                                            TextAnchor.MiddleLeft, "PreviewRow");
                UIFactory.Size(label.gameObject, -1, 30, -1, 30);
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

                _previewRows[i].text =
                    $"{shelf.displayName}　{shelf.product.displayName}　货架 {shelf.count}/{shelf.capacity}　→ 可补到 {projected}";

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
                             new Vector2(0, 46), new Vector2(220, 50));

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
    }
}
