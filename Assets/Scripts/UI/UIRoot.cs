using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MonsterMart.Core;
using MonsterMart.Customers;
using MonsterMart.Data;
using MonsterMart.Store;

namespace MonsterMart.UI
{
    /// <summary>所有模态面板的基类。</summary>
    public abstract class UIPanel : MonoBehaviour
    {
        public RectTransform Root { get; protected set; }

        /// <summary>打开时是否屏蔽世界输入（玩家移动 / 交互）。</summary>
        public virtual bool BlocksWorld => true;

        /// <summary>Esc 能否关闭。</summary>
        public virtual bool CanCloseWithEscape => true;

        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public virtual void Open()
        {
            if (Root != null) Root.gameObject.SetActive(true);
            Root?.SetAsLastSibling();
        }

        public virtual void Close()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// UI 总控 — 设计文档 §10。构建 Canvas，管理营业界面、营业前界面、
    /// 收银界面、结算界面、图鉴、暂停菜单和事件选择弹窗。
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public HudView Hud { get; private set; }

        /// <summary>晨间需求与排班 — 设计文档 §2.1 阶段一。</summary>
        public MorningBriefView MorningBrief { get; private set; }

        public PreparationView Preparation { get; private set; }
        public StockRoomView StockRoomPicker { get; private set; }
        public CheckoutView CheckoutPanel { get; private set; }
        public SettlementView Settlement { get; private set; }
        public EndingView Ending { get; private set; }
        public PauseView Pause { get; private set; }
        public BestiaryView Bestiary { get; private set; }
        public ChoiceDialog Choice { get; private set; }

        /// <summary>远征抬头信息 — 设计文档 §12.2。</summary>
        public ExpeditionView Expedition { get; private set; }

        /// <summary>远征队员信息面板 —— 攻击力/物理/魔法/技能/HP/MP/经验/等级一次看全。</summary>
        public SquadInfoView SquadInfo { get; private set; }

        readonly List<UIPanel> _panels = new List<UIPanel>();

        /// <summary>任何一个模态面板打开时，世界输入都要停下来。</summary>
        public bool BlocksWorldInput
        {
            get
            {
                for (int i = 0; i < _panels.Count; i++)
                    if (_panels[i].IsOpen && _panels[i].BlocksWorld) return true;
                return false;
            }
        }

        public void Build()
        {
            BuildCanvas();
            BuildEventSystem();

            Hud = CreateView<HudView>("HUD");
            MorningBrief = CreateView<MorningBriefView>("MorningBriefView");
            Preparation = CreateView<PreparationView>("PreparationView");
            StockRoomPicker = CreateView<StockRoomView>("StockRoomView");
            CheckoutPanel = CreateView<CheckoutView>("CheckoutView");
            Settlement = CreateView<SettlementView>("SettlementView");
            Ending = CreateView<EndingView>("EndingView");
            Pause = CreateView<PauseView>("PauseView");
            Bestiary = CreateView<BestiaryView>("BestiaryView");
            Choice = CreateView<ChoiceDialog>("ChoiceDialog");
            Expedition = CreateView<ExpeditionView>("ExpeditionView");
            SquadInfo = CreateView<SquadInfoView>("SquadInfoView");

            Hud.BuildUI(Canvas.transform);
            MorningBrief.BuildUI(Canvas.transform);
            Preparation.BuildUI(Canvas.transform);
            StockRoomPicker.BuildUI(Canvas.transform);
            CheckoutPanel.BuildUI(Canvas.transform);
            Settlement.BuildUI(Canvas.transform);
            Ending.BuildUI(Canvas.transform);
            Pause.BuildUI(Canvas.transform);
            Bestiary.BuildUI(Canvas.transform);
            Choice.BuildUI(Canvas.transform);
            Expedition.BuildUI(Canvas.transform);
            SquadInfo.BuildUI(Canvas.transform);

            _panels.Add(MorningBrief);
            _panels.Add(Preparation);
            _panels.Add(StockRoomPicker);
            _panels.Add(CheckoutPanel);
            _panels.Add(Settlement);
            _panels.Add(Ending);
            _panels.Add(Pause);
            _panels.Add(Bestiary);
            _panels.Add(Choice);
            _panels.Add(Expedition);
            _panels.Add(SquadInfo);

            for (int i = 0; i < _panels.Count; i++) _panels[i].Close();
        }

        void BuildCanvas()
        {
            var go = new GameObject("Canvas");
            go.transform.SetParent(transform, false);

            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);   // 文档 §1.4
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
        }

        void BuildEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        T CreateView<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        // ------------------------------------------------------------------
        // 面板控制
        // ------------------------------------------------------------------
        public void ShowMorningBrief() => MorningBrief.OpenFor();
        public void CloseMorningBrief() => MorningBrief.Close();

        public void ShowPreparation() => Preparation.OpenFor(Game.Day.CurrentPlan);
        public void ClosePreparation() => Preparation.Close();

        public void ShowStockRoom() => StockRoomPicker.OpenPicker();

        public void ShowCheckout(Checkout checkout, CustomerController customer)
            => CheckoutPanel.OpenSession(checkout, customer);

        public void CloseCheckout() => CheckoutPanel.Close();

        public void ShowExpedition() => Expedition.Open();
        public void CloseExpedition() => Expedition.Close();

        public void ToggleSquadInfo()
        {
            if (SquadInfo.IsOpen) SquadInfo.Close();
            else SquadInfo.Open();
        }

        public void ShowSettlement(DaySummary summary) => Settlement.OpenFor(summary);

        public void ShowEnding(EndingType ending, string text) => Ending.OpenFor(ending, text);

        public void ShowPauseMenu() => Pause.Open();
        public void ClosePauseMenu() => Pause.Close();

        public void ToggleBestiary()
        {
            if (Bestiary.IsOpen) Bestiary.Close();
            else Bestiary.OpenFor();
        }

        /// <summary>弹出一个选择框；每个选项是 (文案, 回调)。</summary>
        public void ShowChoice(string title, string body, params ChoiceOption[] options)
            => Choice.OpenFor(title, body, options);

        /// <summary>Esc 优先关掉最上层可关面板；返回是否真的关了一个。</summary>
        public bool TryCloseTopPanel()
        {
            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                var panel = _panels[i];
                if (panel.IsOpen && panel.CanCloseWithEscape)
                {
                    panel.Close();
                    return true;
                }
            }
            return false;
        }

        public void CloseAllPanels()
        {
            for (int i = 0; i < _panels.Count; i++) _panels[i].Close();
        }
    }

    /// <summary>事件选择弹窗的一个选项。</summary>
    public struct ChoiceOption
    {
        public string caption;
        public string hint;
        public System.Action action;

        public ChoiceOption(string caption, string hint, System.Action action)
        {
            this.caption = caption;
            this.hint = hint;
            this.action = action;
        }
    }
}
