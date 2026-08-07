using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Expeditions;

namespace MonsterMart.UI
{
    /// <summary>
    /// 远征队员信息面板 —— 用户反馈明确要求「有个按钮能查看所有远征队员信息：
    /// 攻击力、物理/魔法、技能、伤害、HP/MP、经验值、等级」。
    ///
    /// 普攻算物理、技能算魔法（技能本来就和普攻分开结算、不吃精英护甲），
    /// MP 是把技能冷却包装成的展示数值（StaffFollower.ManaPercent），
    /// 不是一套新的资源系统，技能依旧是纯冷却驱动。
    /// </summary>
    public class SquadInfoView : UIPanel
    {
        Transform _list;
        readonly List<Text> _entries = new List<Text>();

        public void BuildUI(Transform canvas)
        {
            Root = UIFactory.NewRect("SquadInfoView", canvas);
            UIFactory.Stretch(Root);

            var scrim = UIFactory.Panel(Root, UIFactory.Scrim, "Scrim");
            UIFactory.Stretch(scrim.rectTransform);

            var window = UIFactory.Panel(Root, UIFactory.PanelBg, "Window");
            UIFactory.Anchor(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(1200, 820));

            var title = UIFactory.Label(window.transform, "远征队员信息", 34, UIFactory.Accent,
                                        TextAnchor.MiddleLeft, "Title");
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -46), new Vector2(-70, 44));

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

        public override void Open()
        {
            base.Open();
            Refresh();
        }

        void Refresh()
        {
            var expedition = Game.Expedition;
            int rows = 1 + (expedition != null ? expedition.Squad.Count : 0);   // 队长 + 员工

            while (_entries.Count < rows)
            {
                var panel = UIFactory.Panel(_list, new Color(1f, 1f, 1f, 0.05f), "Entry");
                UIFactory.Size(panel.gameObject, -1, 118, -1, 118);

                var label = UIFactory.Label(panel.transform, "", 19, UIFactory.Ink,
                                            TextAnchor.UpperLeft, "Text");
                label.lineSpacing = 1.15f;
                UIFactory.Stretch(label.rectTransform, 16, 8, 16, 8);
                _entries.Add(label);
            }

            for (int i = 0; i < _entries.Count; i++)
                _entries[i].transform.parent.gameObject.SetActive(i < rows);

            if (expedition == null) return;

            int index = 0;

            if (expedition.Captain != null && index < _entries.Count)
            {
                var captain = expedition.Captain;
                float hp = captain.Health != null ? captain.Health.Current : 0f;
                float hpMax = captain.Health != null ? captain.Health.Max : 0f;

                _entries[index++].text =
                    $"<b>队长（你）</b>　<color=#C8A8F0>Lv.{CaptainProgress.Level}</color>\n" +
                    $"<color=#F26B61>HP</color> {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(hpMax)}　　" +
                    $"<color=#8FE3C0>携带容量</color> {ExpeditionManager.BagCapacity}" +
                    $"（基础 {ExpeditionManager.BaseBagCapacity} + 升级 {CaptainProgress.CapacityBonus}）\n" +
                    $"<color=#8FA8C8>经验</color> {LevelProgressText(CaptainProgress.Level, CaptainProgress.Xp, CaptainProgress.MaxLevel, CaptainProgress.XpToNext)}";
            }

            var squad = expedition.Squad;
            for (int i = 0; i < squad.Count && index < _entries.Count; i++)
            {
                var member = squad[i];
                if (member == null) continue;

                var entry = StaffRoster.Get(member.Data.staffId);
                float hp = member.Health != null ? member.Health.Current : 0f;
                float hpMax = member.Health != null ? member.Health.Max : 0f;
                string xpText = entry != null
                    ? LevelProgressText(entry.level, entry.xp, StaffRoster.MaxLevel, StaffRoster.XpToNext)
                    : "—";

                _entries[index++].text =
                    $"<b>{member.Data.displayName}</b>　<color=#C8A8F0>Lv.{member.Level}</color>\n" +
                    $"<color=#F26B61>HP</color> {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(hpMax)}　　" +
                    $"<color=#8FA8C8>MP</color> {Mathf.CeilToInt(member.ManaPercent)}/100\n" +
                    $"<color=#FFD966>物理攻击</color> {member.Data.attackDamage:0}　　" +
                    $"<color=#8FA8C8>魔法·{member.Data.skillName}</color> {member.Data.skillDamage:0}\n" +
                    $"<color=#8FA8C8>经验</color> {xpText}";
            }
        }

        static string LevelProgressText(int level, float xp, int maxLevel, System.Func<int, float> xpToNext)
            => level >= maxLevel ? "已满级" : $"{xp:0}/{xpToNext(level):0}";
    }
}
