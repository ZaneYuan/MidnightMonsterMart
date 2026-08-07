using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Core;
using MonsterMart.Data;

// 结算 / 结局 / 暂停 / 图鉴 / 选择弹窗。都是纯展示型面板，放在同一文件。
namespace MonsterMart.UI
{
    /// <summary>营业结算界面 — 设计文档 §10.3。</summary>
    public class SettlementView : UIPanel
    {
        public override bool CanCloseWithEscape => false;

        Text _title;
        Text _numbers;
        Text _customers;
        Text _goals;
        Text _unlocks;
        Button _continueButton;
        Text _continueLabel;

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("SettlementView", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, UIFactory.Scrim, "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(1240, 840));

            _title = UIFactory.Label(window.transform, "第 1 天 · 结算", 40, UIFactory.Accent,
                                     TextAnchor.MiddleCenter, "Title");
            UIFactory.Anchor(_title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                             new Vector2(0, -56), new Vector2(1100, 50));

            _numbers = UIFactory.Label(window.transform, "", 22, UIFactory.Ink, TextAnchor.UpperLeft, "Numbers");
            UIFactory.Anchor(_numbers.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                             new Vector2(330, -300), new Vector2(520, 340));

            _customers = UIFactory.Label(window.transform, "", 22, UIFactory.Ink, TextAnchor.UpperLeft, "Customers");
            UIFactory.Anchor(_customers.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-330, -300), new Vector2(520, 340));

            _goals = UIFactory.Label(window.transform, "", 22, UIFactory.Warn, TextAnchor.UpperLeft, "Goals");
            UIFactory.Anchor(_goals.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                             new Vector2(330, 300), new Vector2(520, 190));

            _unlocks = UIFactory.Label(window.transform, "", 21, UIFactory.Good, TextAnchor.UpperLeft, "Unlocks");
            UIFactory.Anchor(_unlocks.rectTransform, new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-330, 300), new Vector2(520, 190));

            _continueButton = UIFactory.Button(window.transform, "进入下一天", () =>
            {
                Close();
                Game.Manager.ContinueAfterSettlement();
            }, 26, new Color(0.30f, 0.42f, 0.60f));
            UIFactory.Anchor(_continueButton.GetComponent<RectTransform>(),
                             new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 80), new Vector2(320, 60));
            _continueLabel = _continueButton.GetComponentInChildren<Text>();
        }

        public void OpenFor(DaySummary s)
        {
            base.Open();

            _title.text = $"第 {s.day} 天 · 结算";

            _numbers.text =
                "<b>收支</b>\n" +
                $"销售收入　　　{s.salesRevenue}\n" +
                $"已售商品成本　-{s.costOfGoodsSold}\n" +
                $"商品损耗　　　-{s.spoilage}\n" +
                $"维修支出　　　-{s.repairCost}\n" +
                $"<b>当日利润　　　{s.profit}</b>\n\n" +
                $"<size=17><color=#9A9AAE>今晚进货花了 {s.purchaseCost}，没卖掉的算库存，\n" +
                "明天还能继续卖，不计入当日利润。</color></size>\n\n" +
                $"店铺声望　　　{s.reputationBefore} → {s.reputationAfter}\n" +
                $"整洁度　　　　{Mathf.RoundToInt(s.cleanliness)}";

            _customers.text =
                "<b>顾客</b>\n" +
                $"服务顾客数量　{s.served}\n" +
                $"满意顾客数量　{s.happy}\n" +
                $"生气离开　　　{s.leftAngry}\n" +
                $"打烊时未服务　{s.leftUnserved}";

            _goals.text = "<b>今日目标</b>\n" + s.goalReport;
            _goals.color = s.goalsMet ? UIFactory.Good : UIFactory.Warn;

            // 排班的结果和「新解锁」并成一栏 —— 玩家要能把今晚的成绩
            // 和早上那次排班对上号，否则双岗位系统只是个没有反馈的开关
            _unlocks.text = "<b>员工</b>\n" + s.staffReport +
                            "\n\n<b>新解锁</b>\n" + s.unlockNote;

            _continueLabel.text = Game.Day.IsLastDay ? "查看结局" : "进入下一天";
        }
    }

    /// <summary>结局界面 — 设计文档 §8。</summary>
    public class EndingView : UIPanel
    {
        public override bool CanCloseWithEscape => false;

        Text _title;
        Text _body;

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("EndingView", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, new Color(0f, 0f, 0f, 0.88f), "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            _title = UIFactory.Label(Root, "", 52, UIFactory.Accent, TextAnchor.MiddleCenter, "Title");
            UIFactory.Anchor(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0, 220), new Vector2(1200, 80));

            _body = UIFactory.Label(Root, "", 26, UIFactory.Ink, TextAnchor.UpperCenter, "Body");
            UIFactory.Anchor(_body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0, -20), new Vector2(1000, 340));

            var restart = UIFactory.Button(Root, "再开一局", () =>
            {
                Close();
                GameBootstrap.RestartGame();
            }, 26, new Color(0.30f, 0.42f, 0.60f));
            UIFactory.Anchor(restart.GetComponent<RectTransform>(),
                             new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0, -280), new Vector2(300, 60));
        }

        public void OpenFor(EndingType ending, string text)
        {
            base.Open();

            switch (ending)
            {
                case EndingType.Excellent:
                    _title.text = "优秀结局";
                    _title.color = UIFactory.Good;
                    break;
                case EndingType.Normal:
                    _title.text = "普通结局";
                    _title.color = UIFactory.Warn;
                    break;
                default:
                    _title.text = "失败结局";
                    _title.color = UIFactory.Bad;
                    break;
            }

            _body.text = text;
        }
    }

    /// <summary>暂停菜单 — Esc。</summary>
    public class PauseView : UIPanel
    {
        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("PauseView", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, UIFactory.Scrim, "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(520, 480));

            var title = UIFactory.Label(window.transform, "暂停", 40, UIFactory.Accent,
                                        TextAnchor.MiddleCenter, "Title");
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                             new Vector2(0, -56), new Vector2(400, 50));

            var resume = UIFactory.Button(window.transform, "继续游戏", () => Game.Manager.Resume(), 24);
            UIFactory.Anchor(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0, 70), new Vector2(340, 56));

            var bestiary = UIFactory.Button(window.transform, "怪物图鉴", () =>
            {
                Game.UI.ToggleBestiary();
            }, 24);
            UIFactory.Anchor(bestiary.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0, 0), new Vector2(340, 56));

            var restart = UIFactory.Button(window.transform, "重新开始", () => Game.Manager.RestartRun(), 24);
            UIFactory.Anchor(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0, -70), new Vector2(340, 56));

            var quit = UIFactory.Button(window.transform, "退出游戏", Application.Quit, 24,
                                        new Color(0.42f, 0.22f, 0.26f));
            UIFactory.Anchor(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 60), new Vector2(340, 52));
        }
    }

    /// <summary>怪物图鉴 — Tab。设计文档 §2.1 / §13。</summary>
    public class BestiaryView : UIPanel
    {
        Transform _list;
        readonly List<Text> _entries = new List<Text>();

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("BestiaryView", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, UIFactory.Scrim, "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(1100, 860));

            var title = UIFactory.Label(window.transform, "怪物图鉴", 38, UIFactory.Accent,
                                        TextAnchor.MiddleLeft, "Title");
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -50), new Vector2(-70, 46));

            var listRt = UIFactory.NewRect("List", window.transform);
            UIFactory.Stretch(listRt, 40, 100, 40, 100);

            var group = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 10;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;
            _list = listRt;

            var close = UIFactory.Button(window.transform, "关闭 (Tab / Esc)", Close, 22);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 50), new Vector2(280, 52));
        }

        public void OpenFor()
        {
            base.Open();
            Refresh();
        }

        void Refresh()
        {
            var customers = GameDatabase.Customers;

            while (_entries.Count < customers.Count)
            {
                var panel = UIFactory.Panel(_list, new Color(1f, 1f, 1f, 0.05f), "Entry");
                UIFactory.Size(panel.gameObject, -1, 128, -1, 128);

                var label = UIFactory.Label(panel.transform, "", 20, UIFactory.Ink,
                                            TextAnchor.UpperLeft, "Text");
                UIFactory.Stretch(label.rectTransform, 16, 10, 16, 10);
                _entries.Add(label);
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (i >= customers.Count) { _entries[i].text = ""; continue; }

                var data = customers[i];

                if (!BestiaryTracker.IsDiscovered(data.monsterType))
                {
                    // 没见过的客人只留下传闻 —— 预约条里的线索得靠商品名自己推
                    _entries[i].text =
                        "<b>???</b>\n还没有见过这位客人。\n" +
                        $"<color=#8FA8C8>传闻</color>：{data.arrivalClue}";
                    _entries[i].color = UIFactory.InkDim;
                    continue;
                }

                _entries[i].color = UIFactory.Ink;
                _entries[i].text =
                    $"<b>{data.displayName}</b>\n" +
                    $"<color=#7CE07C>喜欢</color>：{ProductNames(GameDatabase.PreferredProducts(data.monsterType))}　　" +
                    $"<color=#F26B61>讨厌</color>：{ProductNames(GameDatabase.DislikedProducts(data.monsterType))}\n" +
                    $"<color=#D89EFF>规则</color>：{data.bestiaryRule}";
            }
        }

        /// <summary>喜欢 / 讨厌列表直接从商品表推导，改数值时不会和图鉴文案脱节。</summary>
        static string ProductNames(List<ProductData> products)
        {
            if (products == null || products.Count == 0) return "—";

            var parts = new string[products.Count];
            for (int i = 0; i < products.Count; i++) parts[i] = products[i].displayName;
            return string.Join("、", parts);
        }
    }

    /// <summary>事件选择弹窗 — 设计文档 §7 的各类玩家选择。</summary>
    public class ChoiceDialog : UIPanel
    {
        public override bool CanCloseWithEscape => false;

        Text _title;
        Text _body;
        Transform _buttonRow;
        readonly List<Button> _buttons = new List<Button>();

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("ChoiceDialog", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, new Color(0f, 0f, 0f, 0.55f), "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(1080, 460));

            _title = UIFactory.Label(window.transform, "", 34, UIFactory.Accent,
                                     TextAnchor.MiddleCenter, "Title");
            UIFactory.Anchor(_title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                             new Vector2(0, -52), new Vector2(980, 46));

            _body = UIFactory.Label(window.transform, "", 24, UIFactory.Ink,
                                    TextAnchor.UpperCenter, "Body");
            UIFactory.Anchor(_body.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                             new Vector2(0, -150), new Vector2(940, 120));

            var rowRt = UIFactory.NewRect("Buttons", window.transform);
            UIFactory.Anchor(rowRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 110), new Vector2(980, 150));

            var group = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 16;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            group.childControlWidth = true;
            group.childControlHeight = true;
            _buttonRow = rowRt;
        }

        public void OpenFor(string title, string body, ChoiceOption[] options)
        {
            base.Open();

            _title.text = title;
            _body.text = body;

            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i] != null) Destroy(_buttons[i].gameObject);
            _buttons.Clear();

            for (int i = 0; i < options.Length; i++)
            {
                var option = options[i];
                int hotkey = i + 1;

                var button = UIFactory.Button(_buttonRow,
                    $"[{hotkey}] {option.caption}\n<size=16><color=#AAAABB>{option.hint}</color></size>",
                    () => Choose(option), 21);
                UIFactory.Size(button.gameObject, 200, 130, 300, 130);
                _buttons.Add(button);
            }
        }

        void Choose(ChoiceOption option)
        {
            Close();
            Game.Audio?.PlayUiClick();
            option.action?.Invoke();
        }

        void Update()
        {
            if (!IsOpen) return;

            int hotkey = InputReader.ChoiceHotkey;
            if (hotkey >= 0 && hotkey < _buttons.Count)
                _buttons[hotkey].onClick.Invoke();
        }
    }
}
