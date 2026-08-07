using UnityEngine;
using MonsterMart.Customers;
using MonsterMart.Data;

namespace MonsterMart.Core
{
    /// <summary>
    /// 游戏总控 — 设计文档 §14.1 GameManager 与 §2.1 的单日循环：
    ///
    ///   晨间需求与排班 → 白天异世界进货 → 闭店准备 → 午夜营业 → 日结 → 下一天
    ///
    /// 一天<b>从晨会开始</b>，不是从备货开始：先看今晚的订单和昨夜的缺货，
    /// 再决定谁出征、谁值夜班，然后那趟远征才有目标可言（§3.1
    /// 「远征不是无目的刷怪，而是为当晚便利店解决供货问题」）。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.MorningBrief;
        public EndingType Ending { get; private set; } = EndingType.None;

        GameState _stateBeforePause;
        int _totalProfit;

        public int TotalProfit => _totalProfit;

        /// <summary>
        /// 今天的远征已经用掉了吗 —— §2.1 一天只有一个白天。
        /// 以前远征是备货界面上一个可以无限点的按钮，等于没有取舍。
        /// </summary>
        public bool ExpeditionDoneToday { get; private set; }

        // ------------------------------------------------------------------
        // 午夜营业倍速 —— 用户反馈明确要求「支持 1/1.25/1.5/2/2.5/3 倍速」。
        //
        // 走全局 Time.timeScale：营业阶段所有靠 Time.deltaTime 推进的系统
        // （顾客移动/耐心、收银判定、补货计时、事件倒计时……）会一起加速，
        // 不用逐个系统手动接倍速参数。跨天保留玩家选的档位，离开营业时归位到
        // 1x，不带进远征/晨会/闭店准备——那几个阶段本来就不该被这个开关影响。
        //
        // 已知取舍：收银的扫描判定窗口也会跟着变快——高倍速下收银会更考手速，
        // 这和大多数模拟经营游戏「倍速=一切都更紧张」的直觉是一致的。
        // ------------------------------------------------------------------
        public static readonly float[] BusinessSpeedOptions = { 1f, 1.25f, 1.5f, 2f, 2.5f, 3f };

        public float BusinessSpeed { get; private set; } = 1f;

        /// <summary>切换营业倍速。正在营业中会立即生效；不在营业中只是记下来，下次开门再用。</summary>
        public void SetBusinessSpeed(float speed)
        {
            BusinessSpeed = speed;
            if (State == GameState.Open) Time.timeScale = speed;
        }

        /// <summary>
        /// <paramref name="totalProfitSoFar"/> 是读档时带回来的前几天累计利润。
        /// 结局判定（EvaluateEnding）比的是三天总利润，不传的话中途退出再进来
        /// 前面几天就白干了，「优秀结局」的 150 利润门槛几乎不可能达成。
        /// </summary>
        public void StartNewRun(int startDay = 1, int totalProfitSoFar = 0)
        {
            ResetRunState(startDay, totalProfitSoFar);
            EnterMorningBrief();
        }

        /// <summary>
        /// 一局的累计状态。和 EnterPreparation() 拆开是因为后者要弹营业前界面，
        /// 而累计利润恰恰是结局判定的输入，得能在没有 UI 的情况下单独验证。
        /// </summary>
        public void ResetRunState(int startDay, int totalProfitSoFar)
        {
            Ending = EndingType.None;
            _totalProfit = totalProfitSoFar;

            Game.Day.SetDay(startDay);
        }

        // ------------------------------------------------------------------
        // 阶段一：晨间需求与排班 — 设计文档 §2.1
        // ------------------------------------------------------------------
        public void EnterMorningBrief()
        {
            BeginNewDay();

            Game.UI?.ShowMorningBrief();
            Game.Audio?.PlayPreparationTheme();

            // 存档点定在每天开工前 —— 一天之内不再存档，
            // 所以中途退出会退回今天早上重来，而不是留下一个「货已到手、
            // 远征却还能再去一趟」的半截存档。
            SaveSystem.Save();
        }

        /// <summary>
        /// 一天开始时的状态重置，不碰 UI —— 和 ResetRunState 一样，
        /// 是为了让无头用例能驱动真实的换日逻辑。
        /// </summary>
        public void BeginNewDay()
        {
            State = GameState.MorningBrief;
            ExpeditionDoneToday = false;

            BestiaryTracker.ClearDailyLog();
            Game.Store.ResetForNewDay();
            Game.Day.PrepareDay();
            Game.Cleanliness.SetValue(GameConfig.CleanlinessMax);
        }

        /// <summary>
        /// 晨会点「出发远征」。返回是否真的出发了。
        /// 一天只能去一趟，且至少要派一个人。
        /// </summary>
        public bool StartDayExpedition()
        {
            if (ExpeditionDoneToday) return false;
            if (Game.Expedition == null || Game.Expedition.IsRunning) return false;

            var squad = StaffRoster.ExpeditionSquad();
            if (squad.Length == 0) return false;

            Game.UI?.CloseMorningBrief();
            Game.Expedition.Begin(squad);
            return true;
        }

        /// <summary>
        /// 晨会点「今天不出门」。远征照样算用掉了 ——
        /// 不出门是一个决定（省下疲劳、但今晚只能卖现有库存），不是跳过。
        /// </summary>
        public void SkipExpedition()
        {
            ExpeditionDoneToday = true;

            Game.UI?.CloseMorningBrief();
            EnterPreparation();
            SaveSystem.Save();
        }

        // ------------------------------------------------------------------
        // 阶段三：闭店准备（无时间限制）
        // ------------------------------------------------------------------
        /// <summary>
        /// 只切状态 + 开界面。当天的重置在 BeginNewDay 里做过了 ——
        /// 远征回来再重置一次会把当天从头来过（污渍、预约条、整洁度）。
        /// </summary>
        public void EnterPreparation()
        {
            State = GameState.Preparation;

            Game.UI?.ShowPreparation();
            Game.Audio?.PlayPreparationTheme();
        }

        // ------------------------------------------------------------------
        // 阶段二：午夜营业
        // ------------------------------------------------------------------
        public void BeginBusiness()
        {
            OpenStore();

            Game.UI?.ClosePreparation();
            Game.Audio?.PlayBusinessTheme();
            Game.UI?.Hud?.Flash($"第 {Game.Day.CurrentDay} 天 · 营业开始");
        }

        // ------------------------------------------------------------------
        // 白天异世界进货 — 设计文档 §3
        // ------------------------------------------------------------------
        /// <summary>出发远征。由 ExpeditionManager.Begin 调用。</summary>
        public void EnterExpedition()
        {
            State = GameState.Expedition;

            Game.UI?.ClosePreparation();
            Game.Audio?.PlayBusinessTheme();
        }

        /// <summary>
        /// 远征结束回到闭店准备。出征的人在这里吃下远征疲劳（§4.4）——
        /// 他们要是今晚还要值夜班，会再吃一份。
        /// </summary>
        public void ReturnFromExpedition()
        {
            ExpeditionDoneToday = true;
            StaffRoster.ApplyExpeditionFatigue();

            EnterPreparation();

            // 一趟远征要跑五六分钟，不能让一次退出把它冲掉。
            // 存档带着 expeditionDoneToday，重进会直接落回闭店准备。
            SaveSystem.Save();
        }

        /// <summary>
        /// 读档时今天的远征已经用掉了 —— 直接落到闭店准备，
        /// 既不让玩家白跑一趟，也不让他带着到手的货再去一趟。
        /// </summary>
        public void ResumeAfterExpedition()
        {
            ExpeditionDoneToday = true;

            Game.UI?.CloseMorningBrief();
            EnterPreparation();
        }

        /// <summary>开门营业的状态部分，不碰 UI —— 方便无头验证。</summary>
        public void OpenStore()
        {
            State = GameState.Open;
            Time.timeScale = BusinessSpeed;

            Game.Day.BeginBusiness();
            Game.Events.BeginDay(Game.Day.CurrentPlan);
        }

        void Update()
        {
            if (!Game.IsReady) return;

            if (State == GameState.Open)
            {
                // 补货岗的怪物员工自己往货架上搬（§4.3「补货」）
                Game.Store.TickStaffRestock(Time.deltaTime);

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
                (State == GameState.Open || State == GameState.Preparation ||
                 State == GameState.MorningBrief))
            {
                Game.UI.ToggleBestiary();
                return;
            }

            // 远征里 Tab 键改查队员信息 —— 图鉴在这个状态下本来就不会打开
            if (InputReader.BestiaryPressed && State == GameState.Expedition)
            {
                Game.UI.ToggleSquadInfo();
                return;
            }

            // 闭店准备阶段可以随时调出进货界面，买完再关掉继续摆货；
            // 营业中也能随时调出来补货（用户反馈明确要求「随时可以补货」）。
            if (InputReader.BuyMenuPressed &&
                (State == GameState.Preparation || State == GameState.Open) &&
                !Game.UI.BlocksWorldInput)
            {
                Game.UI.ShowPreparation();
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
            var summary = ConcludeDay();

            Game.UI?.ShowSettlement(summary);
            Game.Audio?.PlaySettlementTheme();
        }

        /// <summary>
        /// 结算当天并把利润计入本局累计，不碰 UI —— 和 ResetRunState / ConcludeRun
        /// 一样是为了能无头验证「结算存档」这件事本身。
        /// </summary>
        public DaySummary ConcludeDay()
        {
            State = GameState.Settlement;
            Time.timeScale = 1f;   // 倍速不该带出营业阶段，结算/晨会/远征都按正常速度走

            var summary = Game.Day.BuildSummary();
            _totalProfit += summary.profit;

            // 值了一晚夜班的人累积疲劳，休息的人回一点（§4.4）
            StaffRoster.ApplyNightShiftFatigue();

            // 最后一天不在这里存档：它的结算界面后面紧跟着结局
            // （ContinueAfterSettlement → FinishRun 会带 runCompleted 再存一次），
            // 中间这一份没有价值；而一旦存下来，重进时会带着已含最后一天利润的
            // 累计值重打最后一天，结算时再加一遍。
            if (!Game.Day.IsLastDay) SaveSystem.Save();

            return summary;
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
            EnterMorningBrief();
        }

        // ------------------------------------------------------------------
        // 结局 — 设计文档 §8
        // ------------------------------------------------------------------
        void FinishRun()
        {
            ConcludeRun();

            Game.UI?.ShowEnding(Ending, BuildEndingText(Ending));
            Game.Audio?.PlaySettlementTheme();
            SaveSystem.Save();
        }

        /// <summary>
        /// 只把一局收尾（定结局、进 GameOver），不碰 UI —— 和 ResetRunState 一样
        /// 是为了能在没有 UI 的情况下验证：GameOver 是存档里 runCompleted 的来源。
        /// </summary>
        public EndingType ConcludeRun()
        {
            State = GameState.GameOver;
            Ending = EvaluateEnding();
            return Ending;
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
            if (!EnterPauseState()) return;

            Game.UI.ShowPauseMenu();
        }

        /// <summary>只切到暂停状态，不碰 UI。返回是否真的进了暂停。</summary>
        public bool EnterPauseState()
        {
            if (State == GameState.Paused || State == GameState.GameOver) return false;

            _stateBeforePause = State;
            State = GameState.Paused;
            return true;
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

            // 按钮写的是「重新开始」，就得真的从第一天来过 —— 不传 freshRun 的话
            // BootGame 会把本局那份还没打完的存档当进度读回来，变成续玩。
            GameBootstrap.RestartGame(freshRun: true);
        }
    }
}
