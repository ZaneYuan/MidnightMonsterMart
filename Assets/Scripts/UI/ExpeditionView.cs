using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Expeditions;
using MonsterMart.Staff;

namespace MonsterMart.UI
{
    /// <summary>
    /// 远征界面 — 设计文档 §12.2「远征界面」。
    ///
    /// 文档要求：左上小队生命与状态、右上目标商品与携带容量、
    /// 右下技能与撤退。灰盒阶段先把这些做成一条信息栏 + 一排技能条，
    /// 摇杆和触控按钮留到微信适配那一阶段。
    ///
    /// 这是抬头信息，不是模态窗口 —— BlocksWorld 必须是 false，
    /// 否则队长会被自己的 HUD 挡住不能动。
    /// </summary>
    public class ExpeditionView : UIPanel
    {
        public override bool BlocksWorld => false;
        public override bool CanCloseWithEscape => false;

        Text _title;
        Text _objective;
        Text _bag;
        Transform _squadList;
        readonly List<SquadRow> _rows = new List<SquadRow>();

        class SquadRow
        {
            public Text label;
            public Image healthFill;
            public Image cooldownFill;
        }

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("ExpeditionView", canvas);
            UIFactory.Stretch(Root);

            // ---- 左上：小队状态（§12.2）----
            var squadBox = UIFactory.Panel(Root, UIFactory.PanelBgSoft, "SquadBox");
            UIFactory.Anchor(squadBox.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                             new Vector2(250, -150), new Vector2(460, 260));

            _title = UIFactory.Label(squadBox.transform, "暮光森林 · 资源小径", 24, UIFactory.Accent,
                                     TextAnchor.MiddleLeft, "Title");
            UIFactory.Anchor(_title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -28), new Vector2(-32, 32));

            var listRt = UIFactory.NewRect("SquadList", squadBox.transform);
            UIFactory.Stretch(listRt, 16, 16, 16, 56);

            var group = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 6;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childControlWidth = true;
            group.childControlHeight = true;
            _squadList = listRt;

            // ---- 右上：目标与携带 ----
            var goalBox = UIFactory.Panel(Root, UIFactory.PanelBgSoft, "GoalBox");
            UIFactory.Anchor(goalBox.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-260, -110), new Vector2(480, 180));

            _objective = UIFactory.Label(goalBox.transform, "", 20, UIFactory.Ink,
                                         TextAnchor.UpperLeft, "Objective");
            _objective.lineSpacing = 1.2f;
            UIFactory.Anchor(_objective.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -60), new Vector2(-32, 100));

            _bag = UIFactory.Label(goalBox.transform, "", 22, UIFactory.Warn,
                                   TextAnchor.MiddleLeft, "Bag");
            UIFactory.Anchor(_bag.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -28), new Vector2(-32, 30));

            // ---- 底部：操作提示 + 撤退 + 队员信息 ----
            var hint = UIFactory.Label(Root,
                "WASD 移动 · E 采集 · 数字键 1~3 放技能 · Q 标记目标 · R 撤退 · Tab 队员信息",
                20, UIFactory.InkDim, TextAnchor.MiddleCenter, "Hint");
            UIFactory.Anchor(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(0, 62), new Vector2(1200, 34));

            var retreat = UIFactory.Button(Root, "撤退 (R)",
                () => Game.Expedition?.Retreat(), 22, new Color(0.42f, 0.28f, 0.30f));
            UIFactory.Anchor(retreat.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-180, 120), new Vector2(240, 56));

            var squadInfo = UIFactory.Button(Root, "队员信息 (Tab)",
                () => Game.UI?.ToggleSquadInfo(), 22, UIFactory.ButtonBg);
            UIFactory.Anchor(squadInfo.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-430, 120), new Vector2(240, 56));
        }

        public override void Open()
        {
            base.Open();
            RebuildSquadRows();
            Refresh();
        }

        void RebuildSquadRows()
        {
            var squad = Game.Expedition != null ? Game.Expedition.Squad : null;
            int needed = 1 + (squad != null ? squad.Count : 0);   // 队长 + 员工

            while (_rows.Count < needed)
            {
                var rowPanel = UIFactory.Panel(_squadList, new Color(1f, 1f, 1f, 0.05f), "Row");
                UIFactory.Size(rowPanel.gameObject, -1, 52, -1, 52);

                var label = UIFactory.Label(rowPanel.transform, "", 18, UIFactory.Ink,
                                            TextAnchor.UpperLeft, "Name");
                UIFactory.Anchor(label.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                                 new Vector2(0, -14), new Vector2(-20, 24));

                var healthBack = UIFactory.Panel(rowPanel.transform, new Color(0f, 0f, 0f, 0.5f), "HpBack");
                UIFactory.Anchor(healthBack.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                                 new Vector2(0, 16), new Vector2(-20, 10));

                var healthFill = UIFactory.Panel(healthBack.transform, UIFactory.Good, "HpFill");
                UIFactory.Stretch(healthFill.rectTransform);
                healthFill.type = Image.Type.Filled;
                healthFill.fillMethod = Image.FillMethod.Horizontal;

                var cooldownFill = UIFactory.Panel(healthBack.transform, new Color(0.55f, 0.75f, 1f, 0.75f), "CdFill");
                UIFactory.Anchor(cooldownFill.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                                 new Vector2(0, -8), new Vector2(0, 4));
                cooldownFill.type = Image.Type.Filled;
                cooldownFill.fillMethod = Image.FillMethod.Horizontal;

                _rows.Add(new SquadRow
                {
                    label = label,
                    healthFill = healthFill,
                    cooldownFill = cooldownFill,
                });
            }

            for (int i = 0; i < _rows.Count; i++)
                _rows[i].label.transform.parent.gameObject.SetActive(i < needed);
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;

            Refresh();
        }

        void Refresh()
        {
            var expedition = Game.Expedition;
            if (expedition == null) return;

            int rowIndex = 0;

            // 队长
            if (expedition.Captain != null && rowIndex < _rows.Count)
            {
                var row = _rows[rowIndex++];
                float hp = expedition.Captain.Health != null ? expedition.Captain.Health.Normalized : 0f;
                row.label.text = $"队长　<color=#C8A8F0>Lv.{CaptainProgress.Level}</color>　" +
                                  $"{Mathf.CeilToInt(hp * ExpeditionCaptainMaxHealth)} HP";
                row.healthFill.fillAmount = hp;
                row.cooldownFill.fillAmount = 0f;
            }

            var squad = expedition.Squad;
            for (int i = 0; i < squad.Count && rowIndex < _rows.Count; i++)
            {
                var member = squad[i];
                var row = _rows[rowIndex++];
                if (member == null) continue;

                float hp = member.Health != null ? member.Health.Normalized : 0f;
                float fullCooldown = member.EffectiveSkillCooldown;   // §3.6 加班狂热会缩短它
                float cd = fullCooldown <= 0f
                    ? 1f
                    : 1f - Mathf.Clamp01(member.SkillCooldownRemaining / fullCooldown);

                string levelTag = $"<color=#C8A8F0>Lv.{member.Level}</color>";
                row.label.text = member.SkillReady
                    ? $"{i + 1}　{member.Data.displayName}　{levelTag}　<color=#8FE3C0>{member.Data.skillName} 就绪</color>"
                    : $"{i + 1}　{member.Data.displayName}　{levelTag}　{member.Data.skillName} {member.SkillCooldownRemaining:0.0}s";

                row.healthFill.fillAmount = hp;
                row.cooldownFill.fillAmount = cd;
            }

            var room = expedition.CurrentRoom;
            if (room != null)
                _title.text = $"暮光森林 · {room.displayName}　" +
                              $"<color=#8FA8C8>{expedition.RoomIndex + 1}/{expedition.RoomCount}</color>";

            var marked = expedition.MarkedTarget;
            string markLine = marked != null && marked.IsAlive
                ? $"<color=#FFD966>优先攻击：{marked.Data.displayName}</color>"
                : "<color=#8FA8C8>未指定优先目标（Q 切换）</color>";

            string exitLine = !expedition.RoomCleared
                ? $"剩余敌人 {expedition.EnemiesRemaining}"
                : expedition.IsLastRoom
                    ? "<color=#8FE3C0>已清场 · 走到传送点收队回店</color>"
                    : "<color=#8FE3C0>已清场 · 走到传送点进入下一间</color>";

            _objective.text =
                (room != null ? room.briefing + "\n" : "") +
                BossLine(expedition) +
                $"{exitLine}\n{markLine}" +
                BoonLine(expedition);

            // §12.2 要求远征界面显示携带容量
            var node = expedition.HarvestNodeInReach();
            string bagColor = expedition.BagFull ? "#F26B61"
                            : expedition.BagSpaceLeft <= 4 ? "#FFD966" : "#FFFFFF";

            string bag = $"<color={bagColor}>携带 {expedition.BagCount}/{ExpeditionManager.BagCapacity}</color>";

            // 关喷口优先于采集，提示也按同一个优先级走（见 ExpeditionManager.Interact）
            if (expedition.VentInReach() != null)
                _bag.text = bag + "　<color=#C8F080>[E] 关闭孢子喷口</color>";
            else if (node != null)
                _bag.text = bag + $"　<color=#8FE3C0>[E] 采集 {node.Product.displayName} ×{node.Remaining}</color>";
            else
                _bag.text = bag;
        }

        /// <summary>
        /// Boss 的区域机制状态 — §3.3「Boss 通过……关闭装置制造变化」。
        /// 玩家必须一眼看出「现在打它没用，先去关喷口」。
        /// </summary>
        static string BossLine(ExpeditionManager expedition)
        {
            if (expedition.Vents.Count == 0) return "";

            int open = expedition.OpenVentCount;
            if (open > 0)
                return $"<color=#C8F080>孢子喷口 {open}/{expedition.Vents.Count} 开启中 —— " +
                       "巨兽被护盾包着，先走过去按 E 关掉</color>\n";

            return $"<color=#F26B61>护盾消失！{expedition.VentReopenCountdown:0.0} 秒后重新喷发 —— 全力输出</color>\n";
        }

        /// <summary>
        /// 本次远征拿到的强化 — §3.6。每条都带着代价，
        /// 所以要一直挂在界面上，玩家才记得自己是怎么变慢 / 变脆的。
        /// </summary>
        static string BoonLine(ExpeditionManager expedition)
        {
            var boons = expedition.Boons;
            if (boons.Count == 0) return "";

            var text = new System.Text.StringBuilder("\n<color=#C0A0F0>强化：");
            for (int i = 0; i < boons.Count; i++)
            {
                if (i > 0) text.Append(" · ");
                text.Append(boons[i].displayName);
            }
            text.Append("</color>");
            return text.ToString();
        }

        const float ExpeditionCaptainMaxHealth = 120f;
    }
}
