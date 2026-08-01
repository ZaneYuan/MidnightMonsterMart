using UnityEngine;
using MonsterMart.Customers;
using MonsterMart.Data;

namespace MonsterMart.Core
{
    /// <summary>
    /// 游戏总控 — 设计文档 §12.1 GameManager 与 §2.1 的单日四阶段循环：
    /// 营业前准备 → 午夜营业 → 结算 → 升级与剧情 → 下一天。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.Preparation;
        public EndingType Ending { get; private set; } = EndingType.None;

        GameState _stateBeforePause;
        int _totalProfit;

        public int TotalProfit => _totalProfit;

        public void StartNewRun(int startDay = 1)
        {
            Ending = EndingType.None;
            _totalProfit = 0;

            Game.Day.SetDay(startDay);
            EnterPreparation();
        }

        // ------------------------------------------------------------------
        // 阶段一：营业前准备（无时间限制）
        // ------------------------------------------------------------------
        public void EnterPreparation()
        {
            State = GameState.Preparation;

            BestiaryTracker.ClearDailyLog();
            Game.Store.ResetForNewDay();
            Game.Day.PrepareDay();
            Game.Cleanliness.SetValue(GameConfig.CleanlinessMax);

            Game.UI.ShowPreparation();
            Game.Audio?.PlayPreparationTheme();

            // 存档点定在每天开工前，重开时从这一天的准备阶段继续
            SaveSystem.Save();
        }

        // ------------------------------------------------------------------
        // 阶段二：午夜营业
        // ------------------------------------------------------------------
        public void BeginBusiness()
        {
            State = GameState.Open;

            Game.UI.ClosePreparation();
            Game.Day.BeginBusiness();
            Game.Events.BeginDay(Game.Day.CurrentPlan);
            Game.Audio?.PlayBusinessTheme();
            Game.UI.Hud.Flash($"第 {Game.Day.CurrentDay} 天 · 营业开始");
        }

        void Update()
        {
            if (!Game.IsReady) return;

            if (State == GameState.Open)
            {
                if (Game.Day.TickBusiness(Time.deltaTime))
                    CloseStore();
            }

            HandleGlobalHotkeys();
        }

        void HandleGlobalHotkeys()
        {
            if (State == GameState.GameOver) return;

            if (InputReader.PausePressed)
            {
                if (State == GameState.Paused) Resume();
                else if (Game.UI.TryCloseTopPanel()) { /* Esc 先关面板 */ }
                else Pause();
                return;
            }

            if (InputReader.BestiaryPressed &&
                (State == GameState.Open || State == GameState.Preparation))
            {
                Game.UI.ToggleBestiary();
            }
        }

        void CloseStore()
        {
            Game.Spawner.StopDay();
            Game.Spawner.ForceEveryoneOut();
            Game.Events.EndDay();
            Game.UI.CloseCheckout();
            EnterSettlement();
        }

        // ------------------------------------------------------------------
        // 阶段三 + 四：结算 / 升级与剧情
        // ------------------------------------------------------------------
        public void EnterSettlement()
        {
            State = GameState.Settlement;

            var summary = Game.Day.BuildSummary();
            _totalProfit += summary.profit;

            Game.UI.ShowSettlement(summary);
            Game.Audio?.PlaySettlementTheme();
            SaveSystem.Save();
        }

        /// <summary>结算界面点「进入下一天」。</summary>
        public void ContinueAfterSettlement()
        {
            if (Game.Day.IsLastDay)
            {
                FinishRun();
                return;
            }

            Game.Day.AdvanceDay();
            EnterPreparation();
        }

        // ------------------------------------------------------------------
        // 结局 — 设计文档 §8
        // ------------------------------------------------------------------
        void FinishRun()
        {
            State = GameState.GameOver;
            Ending = EvaluateEnding();

            Game.UI.ShowEnding(Ending, BuildEndingText(Ending));
            Game.Audio?.PlaySettlementTheme();
            SaveSystem.Save();
        }

        EndingType EvaluateEnding()
        {
            int rep = Game.Reputation.Value;
            var grade = Game.Day.InspectionResult;

            if (rep < GameConfig.EndingFailureReputation || grade == InspectionGrade.Suspended)
                return EndingType.Failure;

            if (rep >= GameConfig.EndingExcellentReputation &&
                _totalProfit >= GameConfig.EndingExcellentProfit &&
                grade == InspectionGrade.A)
                return EndingType.Excellent;

            return EndingType.Normal;
        }

        string BuildEndingText(EndingType ending)
        {
            string stats =
                $"\n\n三天累计利润：{_totalProfit}\n" +
                $"最终声望：{Game.Reputation.Value}（{Game.Reputation.Tier}）\n" +
                $"检查评价：{Game.Day.InspectionResult}";

            switch (ending)
            {
                case EndingType.Excellent:
                    return "「你的便利店获得了午夜营业许可证。」" + stats;
                case EndingType.Normal:
                    return "「便利店暂时保住了营业资格。」" + stats;
                default:
                    return "「午夜商业管理局要求你停业整改。」" + stats;
            }
        }

        // ------------------------------------------------------------------
        // 暂停
        // ------------------------------------------------------------------
        public void Pause()
        {
            if (State == GameState.Paused || State == GameState.GameOver) return;

            _stateBeforePause = State;
            State = GameState.Paused;
            Game.UI.ShowPauseMenu();
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;

            State = _stateBeforePause;
            Game.UI.ClosePauseMenu();
        }

        /// <summary>从暂停菜单重开。</summary>
        public void RestartRun()
        {
            Game.UI.CloseAllPanels();
            GameBootstrap.RestartGame();
        }
    }
}
