using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.UI
{
    /// <summary>
    /// 晨间需求与排班 — 设计文档 §2.1 阶段一：
    /// 「玩家先查看昨夜缺货、顾客订单、天气、月相和员工状态，然后决定今日人员分配。」
    ///
    /// 这一屏是整个昼夜循环的枢纽：右边排班决定了谁去远征、谁值夜班，
    /// 左边的预约条决定了这趟远征该去找什么货（§3.1「远征不是无目的刷怪，
    /// 而是为当晚便利店解决供货问题」）。
    /// </summary>
    public class MorningBriefView : UIPanel
    {
        public override bool CanCloseWithEscape => false;

        Text _title;
        Text _notes;
        Text _statusLine;
        Text _squadHint;
        Transform _rosterList;
        Button _goButton;
        Text _goLabel;

        readonly List<RosterRow> _rows = new List<RosterRow>();

        class RosterRow
        {
            public string staffId;
            public Text name;
            public Text ability;
            public Image expeditionBg;
            public Text expeditionLabel;
            public Text nightJobLabel;
            public Image fatigueFill;
            public Text fatigueLabel;
            public Image xpFill;
        }

        const float WindowW = 1500f;
        const float WindowH = 900f;
        const float RowHeight = 96f;

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("MorningBriefView", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, UIFactory.Scrim, "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(WindowW, WindowH));

            BuildHeader(window.transform);
            BuildNotesColumn(window.transform);
            BuildRosterColumn(window.transform);
            BuildFooter(window.transform);
        }

        void BuildHeader(Transform window)
        {
            _title = UIFactory.Label(window, "第 1 天 · 晨间简报", 38, UIFactory.Accent,
                                     TextAnchor.MiddleLeft, "Title");
            UIFactory.Anchor(_title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -46), new Vector2(-80, 48));

            _statusLine = UIFactory.Label(window, "", 20, UIFactory.Warn,
                                          TextAnchor.MiddleLeft, "Status");
            UIFactory.Anchor(_statusLine.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -92), new Vector2(-80, 32));
        }

        const float ColumnTop = -130f;
        const float ColumnHeight = 620f;

        void BuildNotesColumn(Transform window)
        {
            var box = UIFactory.Panel(window, UIFactory.PanelBgSoft, "NotesBox");
            UIFactory.Anchor(box.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                             new Vector2(340, ColumnTop - ColumnHeight * 0.5f),
                             new Vector2(640, ColumnHeight));

            var header = UIFactory.Label(box.transform, "今晚的预约条", 24, UIFactory.Accent,
                                         TextAnchor.MiddleLeft, "Header");
            UIFactory.Anchor(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -26), new Vector2(-32, 32));

            _notes = UIFactory.Label(box.transform, "", 19, UIFactory.Ink,
                                     TextAnchor.UpperLeft, "Notes");
            _notes.lineSpacing = 1.25f;
            UIFactory.Anchor(_notes.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -(ColumnHeight * 0.5f) - 6f),
                             new Vector2(-40, ColumnHeight - 70f));
        }

        void BuildRosterColumn(Transform window)
        {
            var box = UIFactory.Panel(window, UIFactory.PanelBgSoft, "RosterBox");
            UIFactory.Anchor(box.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-420, ColumnTop - ColumnHeight * 0.5f),
                             new Vector2(800, ColumnHeight));

            var header = UIFactory.Label(box.transform, "排班", 24, UIFactory.Accent,
                                         TextAnchor.MiddleLeft, "Header");
            UIFactory.Anchor(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -26), new Vector2(-32, 32));

            _squadHint = UIFactory.Label(box.transform, "", 19, UIFactory.Warn,
                                         TextAnchor.MiddleRight, "SquadHint");
            UIFactory.Anchor(_squadHint.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -26), new Vector2(-32, 32));

            var listRt = UIFactory.NewRect("Roster", box.transform);
            UIFactory.Stretch(listRt, 16, 16, 16, 56);

            var group = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 8;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;

            _rosterList = listRt;
            BuildRosterRows();
        }

        void BuildRosterRows()
        {
            _rows.Clear();

            var staff = GameDatabase.Staff;
            for (int i = 0; i < staff.Count; i++)
            {
                var data = staff[i];
                string id = data.staffId;

                var rowPanel = UIFactory.Panel(_rosterList, new Color(1f, 1f, 1f, 0.05f), "Row");
                UIFactory.Size(rowPanel.gameObject, -1, RowHeight, -1, RowHeight);

                var name = UIFactory.Label(rowPanel.transform, data.displayName, 21, UIFactory.Ink,
                                           TextAnchor.UpperLeft, "Name");
                UIFactory.Anchor(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                                 new Vector2(150, -22), new Vector2(280, 28));

                // 打怪升级：一条紧贴名字下方的经验条，一眼看出这只怪物练到哪了
                var xpBack = UIFactory.Panel(rowPanel.transform, new Color(0f, 0f, 0f, 0.35f), "XpBack");
                UIFactory.Anchor(xpBack.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                                 new Vector2(150, -50), new Vector2(150, 6));

                var xpFill = UIFactory.Panel(xpBack.transform, new Color(0.78f, 0.62f, 0.95f), "XpFill");
                UIFactory.Stretch(xpFill.rectTransform);
                xpFill.type = Image.Type.Filled;
                xpFill.fillMethod = Image.FillMethod.Horizontal;

                // 远征功能 / 店内功能 —— §4.1 要求玩家能同时看到两边再决定
                var ability = UIFactory.Label(rowPanel.transform, "", 15, UIFactory.InkDim,
                                              TextAnchor.UpperLeft, "Ability");
                ability.lineSpacing = 1.1f;
                UIFactory.Anchor(ability.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                                 new Vector2(300, -60), new Vector2(580, 44));

                var fatigueBack = UIFactory.Panel(rowPanel.transform, new Color(0f, 0f, 0f, 0.45f),
                                                  "FatigueBack");
                UIFactory.Anchor(fatigueBack.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                                 new Vector2(220, -26), new Vector2(150, 12));

                var fatigueFill = UIFactory.Panel(fatigueBack.transform, UIFactory.Good, "FatigueFill");
                UIFactory.Stretch(fatigueFill.rectTransform);
                fatigueFill.type = Image.Type.Filled;
                fatigueFill.fillMethod = Image.FillMethod.Horizontal;

                var fatigueLabel = UIFactory.Label(rowPanel.transform, "", 15, UIFactory.InkDim,
                                                   TextAnchor.MiddleLeft, "FatigueLabel");
                UIFactory.Anchor(fatigueLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                                 new Vector2(430, -26), new Vector2(260, 24));

                // 两个独立的轴：白天去不去远征、晚上站哪个岗（§4.4 允许两边都占）
                var expeditionButton = UIFactory.Button(rowPanel.transform, "白天出征",
                                                        () => OnToggleExpedition(id), 18,
                                                        UIFactory.ButtonBg);
                UIFactory.Anchor(expeditionButton.GetComponent<RectTransform>(),
                                 new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                                 new Vector2(-250, 0), new Vector2(160, 54));

                var nightButton = UIFactory.Button(rowPanel.transform, "不值班",
                                                   () => OnCycleNightJob(id), 18,
                                                   UIFactory.ButtonBg);
                UIFactory.Anchor(nightButton.GetComponent<RectTransform>(),
                                 new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                                 new Vector2(-80, 0), new Vector2(160, 54));

                _rows.Add(new RosterRow
                {
                    staffId = id,
                    name = name,
                    ability = ability,
                    expeditionBg = expeditionButton.GetComponent<Image>(),
                    expeditionLabel = expeditionButton.GetComponentInChildren<Text>(),
                    nightJobLabel = nightButton.GetComponentInChildren<Text>(),
                    fatigueFill = fatigueFill,
                    fatigueLabel = fatigueLabel,
                    xpFill = xpFill,
                });
            }
        }

        void BuildFooter(Transform window)
        {
            _goButton = UIFactory.Button(window, "出发远征", OnGo, 26,
                                         new Color(0.30f, 0.46f, 0.36f));
            UIFactory.Anchor(_goButton.GetComponent<RectTransform>(), new Vector2(1, 0),
                             new Vector2(1, 0), new Vector2(-220, 70), new Vector2(340, 66));
            _goLabel = _goButton.GetComponentInChildren<Text>();

            var skip = UIFactory.Button(window, "今天不出门，直接备货", OnSkip, 21,
                                        UIFactory.ButtonBg);
            UIFactory.Anchor(skip.GetComponent<RectTransform>(), new Vector2(1, 0),
                             new Vector2(1, 0), new Vector2(-560, 70), new Vector2(320, 66));

            var hint = UIFactory.Label(window,
                "左边看预约条猜今晚谁会来，右边决定谁去异世界、谁站夜班岗。\n" +
                "同一个人可以白天出征、晚上照样上岗 —— 但会连轴转，第二天效率大跌。" +
                "一天只有一趟远征。",
                18, UIFactory.InkDim, TextAnchor.MiddleLeft, "Hint");
            UIFactory.Anchor(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0),
                             new Vector2(470, 70), new Vector2(880, 60));
        }

        // ------------------------------------------------------------------
        public void OpenFor()
        {
            base.Open();
            Refresh();
        }

        void OnToggleExpedition(string staffId)
        {
            var entry = StaffRoster.Get(staffId);
            bool wanted = entry != null && !entry.onExpedition;

            bool now = StaffRoster.ToggleExpedition(staffId);

            // 队伍满员时想加人会被拒，给个提示，别让玩家以为按钮坏了
            if (wanted && !now)
            {
                Game.Audio?.PlayError();
                Game.UI?.Hud?.Flash($"远征队最多 {StaffRoster.MaxSquadSize} 人，先把谁换下来");
            }
            else
            {
                Game.Audio?.PlayUiClick();
            }

            Refresh();
        }

        void OnCycleNightJob(string staffId)
        {
            StaffRoster.CycleNightJob(staffId);
            Game.Audio?.PlayUiClick();
            Refresh();
        }

        void OnGo()
        {
            if (Game.Manager == null) return;

            if (!Game.Manager.StartDayExpedition())
            {
                Game.Audio?.PlayError();
                Game.UI?.Hud?.Flash("至少要派一个人出征，或者选「今天不出门」");
            }
        }

        void OnSkip() => Game.Manager?.SkipExpedition();

        void Refresh()
        {
            var day = Game.Day;
            if (day == null) return;

            _title.text = $"第 {day.CurrentDay} 天 · 晨间简报";

            _statusLine.text =
                $"资金 {Game.Economy?.Money ?? 0}　·　声望 {Game.Reputation?.Value ?? 0}" +
                $"　·　仓库存货 {WarehouseTotal()} 件" +
                (day.CurrentPlan != null && day.CurrentPlan.fullMoon
                    ? "　·　<color=#FFD966>今晚满月</color>"
                    : "");

            _notes.text = BuildNotesText();

            int squad = StaffRoster.SquadSize;
            _squadHint.text =
                $"远征队 {squad}/{StaffRoster.MaxSquadSize}　·　今晚 {NightJobSummary()}";

            RefreshRoster();

            bool canGo = squad > 0 && !Game.Manager.ExpeditionDoneToday;
            _goButton.interactable = canGo;
            _goLabel.text = squad > 0 ? $"出发远征（{squad} 人）" : "没人出征";
        }

        /// <summary>今晚哪些岗位有人 —— 没人管的岗位要显眼，那是真的会出事的。</summary>
        static string NightJobSummary()
        {
            var jobs = new[]
            {
                StaffAssignment.Cashier,
                StaffAssignment.Restock,
                StaffAssignment.Security,
            };

            var parts = new List<string>();
            for (int i = 0; i < jobs.Length; i++)
            {
                string label = StaffRoster.NightJobLabel(jobs[i]);
                parts.Add(StaffRoster.AnyOn(jobs[i])
                    ? $"<color=#8FE3C0>{label}</color>"
                    : $"<color=#F26B61>{label}✗</color>");
            }
            return string.Join(" ", parts);
        }

        int WarehouseTotal()
        {
            if (Game.Store == null) return 0;
            int n = 0;
            for (int i = 0; i < GameDatabase.Products.Count; i++)
                n += Game.Store.WarehouseCount(GameDatabase.Products[i]);
            return n;
        }

        string BuildNotesText()
        {
            var sb = new StringBuilder();

            var notes = Game.Day.Notes;
            if (notes != null && notes.Count > 0)
            {
                for (int i = 0; i < notes.Count; i++)
                    sb.AppendLine($"<color=#C8B4F0>·</color> {notes[i].text}");
            }
            else
            {
                sb.AppendLine("（今晚没有预约条）");
            }

            sb.AppendLine();
            sb.AppendLine("<color=#8FA8C8>货架现状</color>");

            int empty = Game.Store != null ? Game.Store.EmptyShelfCount() : 0;
            int sales = Game.Store != null ? Game.Store.SalesShelfCount() : 0;
            sb.AppendLine(empty > 0
                ? $"<color=#F26B61>{empty}/{sales} 个货架是空的</color> —— 今晚这些位置卖不出东西"
                : $"{sales} 个货架都有货");

            if (ExpeditionProgress.ColdShelfCores > 0)
                sb.AppendLine($"\n<color=#8FE3C0>后仓有冷藏货架核心 ×{ExpeditionProgress.ColdShelfCores}</color>");

            return sb.ToString();
        }

        void RefreshRoster()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var entry = StaffRoster.Get(row.staffId);
                if (entry == null || entry.Data == null) continue;

                var data = entry.Data;

                // 打怪升级：名字后面挂一个等级徽章，经验条压在名字正下方，一眼就是「练到哪了」
                row.name.text = $"{data.displayName}　<color=#C8A8F0>Lv.{entry.level}</color>";
                row.xpFill.fillAmount = entry.level >= StaffRoster.MaxLevel
                    ? 1f
                    : Mathf.Clamp01(entry.xp / StaffRoster.XpToNext(entry.level));

                row.expeditionLabel.text = entry.onExpedition ? "✓ 白天出征" : "白天留店";
                row.expeditionBg.color = entry.onExpedition
                    ? new Color(0.30f, 0.46f, 0.36f)
                    : UIFactory.ButtonBg;

                row.nightJobLabel.text = StaffRoster.NightJobLabel(entry.nightJob);

                // §4.1 要求玩家能同时看到远征功能和店内功能再决定，所以两条都摆出来
                row.ability.text =
                    $"<color=#8FE3C0>远征</color> {data.expeditionPassive}\n" +
                    $"<color=#8FA8C8>店内</color> {data.storeAbility}";

                float t = Mathf.Clamp01(entry.fatigue / StaffRoster.MaxFatigue);
                row.fatigueFill.fillAmount = t;
                row.fatigueFill.color = StaffRoster.IsExhausted(entry)
                    ? UIFactory.Bad
                    : t > 0.4f ? UIFactory.Warn : UIFactory.Good;

                int efficiency = Mathf.RoundToInt(StaffRoster.Efficiency(entry) * 100f);
                string warning = entry.IsDoubleShift
                    ? "　<color=#F26B61>连轴转</color>"
                    : entry.IsFullyResting ? "　<color=#8FE3C0>今天休息</color>" : "";

                row.fatigueLabel.text =
                    $"{StaffRoster.FatigueLabel(entry)}　效率 {efficiency}%{warning}";
            }
        }
    }
}
