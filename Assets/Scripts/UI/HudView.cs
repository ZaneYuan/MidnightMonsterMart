using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.UI
{
    /// <summary>营业界面 HUD — 设计文档 §10.1。</summary>
    public class HudView : MonoBehaviour
    {
        Text _dayLabel;
        Text _clockLabel;
        Image _timeBar;

        Text _moneyLabel;
        Text _reputationLabel;
        Text _cleanlinessLabel;

        Image _carryIcon;
        Text _carryLabel;
        Text _promptLabel;
        Text _hintLabel;

        Image _holdBar;
        RectTransform _holdRoot;

        Image _blackout;
        RectTransform _toastRoot;

        RectTransform _prepBar;
        Text _prepHint;

        readonly List<ToastEntry> _toasts = new List<ToastEntry>();

        class ToastEntry
        {
            public Text label;
            public float life;
        }

        public void BuildUI(Transform canvas)
        {
            var root = UIFactory.NewRect("HUD", canvas);
            UIFactory.Stretch(root);

            // 停电遮罩放在最底层，盖住世界但不盖住 HUD
            _blackout = UIFactory.Panel(root, new Color(0f, 0f, 0.02f, 0f), "BlackoutOverlay");
            UIFactory.Stretch(_blackout.rectTransform);
            _blackout.raycastTarget = false;

            BuildTopLeft(root);
            BuildTopRight(root);
            BuildBottom(root);
            BuildHoldBar(root);
            BuildPreparationBar(root);
            BuildToasts(root);
        }

        /// <summary>
        /// 准备阶段的常驻工具条。关掉进货面板后可以自由走动摆货，
        /// 摆完再从这里开门 —— 设计文档 §2.1「准备阶段没有时间限制」。
        /// </summary>
        void BuildPreparationBar(RectTransform root)
        {
            var box = UIFactory.Panel(root, UIFactory.PanelBg, "PrepBar");
            UIFactory.Anchor(box.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                             new Vector2(0, -70), new Vector2(920, 92));
            _prepBar = box.rectTransform;

            _prepHint = UIFactory.Label(box.transform,
                "营业前准备：去仓库拿货，走到对应货架前长按 E 摆上去。没有时间限制。",
                20, UIFactory.Ink, TextAnchor.MiddleLeft, "Hint");
            UIFactory.Anchor(_prepHint.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                             new Vector2(268, 0), new Vector2(500, 60));

            var buy = UIFactory.Button(box.transform, "进货界面 (B)",
                () => Game.UI.ShowPreparation(), 20);
            UIFactory.Anchor(buy.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                             new Vector2(-290, 0), new Vector2(180, 56));

            var open = UIFactory.Button(box.transform, "开始营业",
                () => Game.UI.Preparation.TryBeginBusiness(), 22, new Color(0.30f, 0.52f, 0.34f));
            UIFactory.Anchor(open.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                             new Vector2(-110, 0), new Vector2(180, 56));

            _prepBar.gameObject.SetActive(false);
        }

        void BuildTopLeft(RectTransform root)
        {
            var box = UIFactory.Panel(root, UIFactory.PanelBg, "TopLeft");
            UIFactory.Anchor(box.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                             new Vector2(180, -66), new Vector2(330, 110));

            _dayLabel = UIFactory.Label(box.transform, "第 1 天", 30, UIFactory.Ink, TextAnchor.UpperLeft, "Day");
            UIFactory.Anchor(_dayLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -26), new Vector2(-28, 36));

            _clockLabel = UIFactory.Label(box.transform, "营业剩余 00:00", 22, UIFactory.InkDim,
                                          TextAnchor.UpperLeft, "Clock");
            UIFactory.Anchor(_clockLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -60), new Vector2(-28, 30));

            _timeBar = UIFactory.Bar(box.transform, new Color(0.05f, 0.05f, 0.09f, 1f),
                                     UIFactory.Accent, 290, 10, "TimeBar");
            UIFactory.Anchor(_timeBar.rectTransform.parent as RectTransform,
                             new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 16), new Vector2(290, 10));
        }

        void BuildTopRight(RectTransform root)
        {
            var box = UIFactory.Panel(root, UIFactory.PanelBg, "TopRight");
            UIFactory.Anchor(box.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-180, -66), new Vector2(330, 110));

            _moneyLabel = UIFactory.Label(box.transform, "现金 0", 24, UIFactory.Warn,
                                          TextAnchor.MiddleRight, "Money");
            UIFactory.Anchor(_moneyLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(-14, -24), new Vector2(-28, 30));

            _reputationLabel = UIFactory.Label(box.transform, "声望 0", 22, UIFactory.Ink,
                                               TextAnchor.MiddleRight, "Reputation");
            UIFactory.Anchor(_reputationLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(-14, -54), new Vector2(-28, 28));

            _cleanlinessLabel = UIFactory.Label(box.transform, "整洁 100", 22, UIFactory.Good,
                                                TextAnchor.MiddleRight, "Cleanliness");
            UIFactory.Anchor(_cleanlinessLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(-14, -82), new Vector2(-28, 28));
        }

        void BuildBottom(RectTransform root)
        {
            var box = UIFactory.Panel(root, UIFactory.PanelBg, "BottomBar");
            UIFactory.Anchor(box.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 62), new Vector2(1080, 104));

            _carryIcon = UIFactory.Icon(box.transform, null, 56, "CarryIcon");
            UIFactory.Anchor(_carryIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                             new Vector2(48, 12), new Vector2(56, 56));
            _carryIcon.enabled = false;

            _carryLabel = UIFactory.Label(box.transform, "空手", 20, UIFactory.InkDim,
                                          TextAnchor.MiddleCenter, "CarryLabel");
            UIFactory.Anchor(_carryLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                             new Vector2(48, -32), new Vector2(160, 24));

            _promptLabel = UIFactory.Label(box.transform, "", 24, UIFactory.Accent,
                                           TextAnchor.MiddleCenter, "Prompt");
            UIFactory.Anchor(_promptLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(40, 16), new Vector2(700, 32));

            _hintLabel = UIFactory.Label(box.transform,
                "WASD 移动 · Shift 加速 · E 交互 · Tab 图鉴 · Esc 暂停",
                17, new Color(0.55f, 0.55f, 0.66f), TextAnchor.MiddleCenter, "Hint");
            UIFactory.Anchor(_hintLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(40, -22), new Vector2(700, 24));
        }

        void BuildHoldBar(RectTransform root)
        {
            _holdRoot = UIFactory.NewRect("HoldBar", root);
            UIFactory.Anchor(_holdRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 148), new Vector2(300, 16));

            var back = UIFactory.Panel(_holdRoot, new Color(0.05f, 0.05f, 0.09f, 0.92f), "Back");
            UIFactory.Stretch(back.rectTransform);

            var fillRt = UIFactory.NewRect("Fill", back.transform);
            UIFactory.Stretch(fillRt, 2, 2, 2, 2);

            _holdBar = fillRt.gameObject.AddComponent<Image>();
            _holdBar.sprite = UIFactory.White;
            _holdBar.color = UIFactory.Good;
            _holdBar.type = Image.Type.Filled;
            _holdBar.fillMethod = Image.FillMethod.Horizontal;
            _holdBar.fillAmount = 0f;
            _holdBar.raycastTarget = false;

            _holdRoot.gameObject.SetActive(false);
        }

        void BuildToasts(RectTransform root)
        {
            _toastRoot = UIFactory.NewRect("Toasts", root);
            UIFactory.Anchor(_toastRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                             new Vector2(0, -200), new Vector2(900, 220));

            var group = _toastRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 6;
            group.childAlignment = TextAnchor.UpperCenter;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;
        }

        // ------------------------------------------------------------------
        // 刷新
        // ------------------------------------------------------------------
        void Update()
        {
            if (!Game.IsReady) return;

            UpdateStats();
            UpdateCarry();
            UpdatePrompt();
            UpdatePreparationBar();
            UpdateToasts();
        }

        void UpdatePreparationBar()
        {
            if (_prepBar == null) return;

            bool show = Game.Manager.State == GameState.Preparation &&
                        Game.UI != null && !Game.UI.BlocksWorldInput;

            if (_prepBar.gameObject.activeSelf != show) _prepBar.gameObject.SetActive(show);
            if (!show) return;

            int empty = Game.Store != null ? Game.Store.EmptyShelfCount() : 0;
            _prepHint.text = empty > 0
                ? $"营业前准备 · 还有 <color=#F26B61>{empty}</color> 个货架是空的（红标）\n去仓库拿货 → 走到对应货架前<b>长按 E</b>。没有时间限制。"
                : "营业前准备 · 所有货架都有货了，可以开门了。";
        }

        void UpdateStats()
        {
            var day = Game.Day;
            var eco = Game.Economy;
            var rep = Game.Reputation;
            var clean = Game.Cleanliness;

            if (day != null)
            {
                _dayLabel.text = $"第 {day.CurrentDay} 天 · 午夜营业";

                if (Game.Manager.State == GameState.Open)
                {
                    int total = Mathf.CeilToInt(day.TimeRemaining);
                    _clockLabel.text = $"营业剩余 {total / 60:00}:{total % 60:00}";
                    _timeBar.fillAmount = day.BusinessDuration <= 0f
                        ? 0f
                        : Mathf.Clamp01(day.TimeRemaining / day.BusinessDuration);
                }
                else
                {
                    _clockLabel.text = "营业前准备（无时间限制）";
                    _timeBar.fillAmount = 1f;
                }
            }

            if (eco != null) _moneyLabel.text = $"现金 {eco.Money}";

            if (rep != null)
            {
                _reputationLabel.text = $"声望 {rep.Value}";
                _reputationLabel.color = rep.Value >= 60 ? UIFactory.Good
                                       : rep.Value >= 30 ? UIFactory.Warn : UIFactory.Bad;
            }

            if (clean != null)
            {
                _cleanlinessLabel.text = $"整洁 {Mathf.RoundToInt(clean.Value)}";
                _cleanlinessLabel.color = clean.IsFilthy ? UIFactory.Bad
                                        : clean.IsDirty ? UIFactory.Warn : UIFactory.Good;
            }
        }

        void UpdateCarry()
        {
            var player = Game.Player;
            if (player == null) return;

            if (player.Carry.IsEmpty)
            {
                _carryIcon.enabled = false;
                _carryLabel.text = "空手";
                _carryLabel.color = UIFactory.InkDim;
                return;
            }

            _carryIcon.enabled = true;
            _carryIcon.sprite = Art.SpriteFactory.ProductIcon(player.Carry.Product);
            _carryLabel.color = UIFactory.Ink;

            if (player.Carry.Packed)
            {
                _carryLabel.text = $"{player.Carry.Product.displayName} ×{player.Carry.Count}\n<size=15><color=#D89EFF>交给幽灵</color></size>";
                return;
            }

            // 直接告诉玩家该往哪个货架送 —— 光有商品名很容易找错货架
            var shelf = Game.Store != null ? Game.Store.FindShelf(player.Carry.Product) : null;
            _carryLabel.text = shelf != null
                ? $"{player.Carry.Product.displayName} ×{player.Carry.Count}\n<size=15><color=#FFD966>送到「{shelf.displayName}」</color></size>"
                : $"{player.Carry.Product.displayName} ×{player.Carry.Count}";
        }

        void UpdatePrompt()
        {
            var player = Game.Player;
            if (player == null) return;

            bool blocked = Game.UI != null && Game.UI.BlocksWorldInput;
            var focus = blocked ? null : player.Focus;

            _promptLabel.text = focus != null ? focus.GetPrompt(player) : "";

            bool holding = player.HoldProgress > 0f;
            if (_holdRoot.gameObject.activeSelf != holding) _holdRoot.gameObject.SetActive(holding);
            if (holding) _holdBar.fillAmount = player.HoldProgress;
        }

        // ------------------------------------------------------------------
        // 提示条
        // ------------------------------------------------------------------
        public void Flash(string message, float seconds = 3.2f)
        {
            if (string.IsNullOrEmpty(message) || _toastRoot == null) return;

            var label = UIFactory.Label(_toastRoot, message, 21, UIFactory.Ink,
                                        TextAnchor.MiddleCenter, "Toast");
            UIFactory.Size(label.gameObject, -1, 28, -1, 28);

            _toasts.Add(new ToastEntry { label = label, life = seconds });

            // 最多同时显示 5 条
            while (_toasts.Count > 5)
            {
                var oldest = _toasts[0];
                _toasts.RemoveAt(0);
                if (oldest.label != null) Destroy(oldest.label.gameObject);
            }
        }

        void UpdateToasts()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var entry = _toasts[i];
                entry.life -= Time.deltaTime;

                if (entry.life <= 0f)
                {
                    if (entry.label != null) Destroy(entry.label.gameObject);
                    _toasts.RemoveAt(i);
                    continue;
                }

                if (entry.label != null && entry.life < 0.7f)
                    entry.label.color = new Color(UIFactory.Ink.r, UIFactory.Ink.g,
                                                  UIFactory.Ink.b, entry.life / 0.7f);
            }
        }

        // ------------------------------------------------------------------
        // 停电遮罩 — 设计文档 §7 事件一
        // ------------------------------------------------------------------
        public void SetBlackout(bool active)
        {
            if (_blackout == null) return;
            _blackout.color = new Color(0f, 0f, 0.02f, active ? 0.72f : 0f);
        }
    }
}
