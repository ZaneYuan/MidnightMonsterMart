using UnityEngine;

namespace MonsterMart.Core
{
    /// <summary>
    /// 输入抽象层 — 设计文档 §3.1 的按键方案。
    ///
    /// 原型使用旧版 Input Manager（零配置，Unity 默认设置即可运行）。
    /// 若要切换到 Input System 包：只需重写这一个文件的内部实现，
    /// 游戏其余部分只认这里暴露的属性，不会受影响。
    /// </summary>
    public static class InputReader
    {
        /// <summary>WASD / 方向键，已归一化。</summary>
        public static Vector2 MoveAxis
        {
            get
            {
                float x = 0f, y = 0f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;

                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        // ------------------------------------------------------------------
        // 加速：双击方向键
        // ------------------------------------------------------------------
        const float DoubleTapWindow = 0.28f;

        static readonly KeyCode[][] DirectionKeys =
        {
            new[] { KeyCode.W, KeyCode.UpArrow },
            new[] { KeyCode.S, KeyCode.DownArrow },
            new[] { KeyCode.A, KeyCode.LeftArrow },
            new[] { KeyCode.D, KeyCode.RightArrow },
        };

        static readonly float[] _lastTapTime = { -10f, -10f, -10f, -10f };
        static bool _sprintLatched;
        static int _evaluatedFrame = -1;

        /// <summary>
        /// 加速状态。双击任一方向键进入，松开所有方向键后退出
        /// （想再加速需要重新双击）。
        /// </summary>
        public static bool Sprint
        {
            get
            {
                Tick();
                return _sprintLatched;
            }
        }

        /// <summary>每帧调用一次，即使玩家没在移动也要调，否则加速状态不会解除。</summary>
        public static void Tick()
        {
            if (_evaluatedFrame == Time.frameCount) return;
            _evaluatedFrame = Time.frameCount;

            // 停下来就取消加速
            if (MoveAxis.sqrMagnitude < 0.0001f)
            {
                _sprintLatched = false;
                return;
            }

            for (int dir = 0; dir < DirectionKeys.Length; dir++)
            {
                if (!AnyKeyDown(DirectionKeys[dir])) continue;

                if (Time.time - _lastTapTime[dir] <= DoubleTapWindow) _sprintLatched = true;
                _lastTapTime[dir] = Time.time;
            }
        }

        static bool AnyKeyDown(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKeyDown(keys[i])) return true;
            return false;
        }

        /// <summary>重开一局时清掉残留状态。</summary>
        public static void Reset()
        {
            _sprintLatched = false;
            _evaluatedFrame = -1;
            for (int i = 0; i < _lastTapTime.Length; i++) _lastTapTime[i] = -10f;
        }

        /// <summary>E：交互（按下瞬间）。</summary>
        public static bool InteractPressed => Input.GetKeyDown(KeyCode.E);

        /// <summary>E：长按交互（补货、扶货架、清理污渍）。</summary>
        public static bool InteractHeld => Input.GetKey(KeyCode.E);

        /// <summary>Tab：打开图鉴。</summary>
        public static bool BestiaryPressed => Input.GetKeyDown(KeyCode.Tab);

        /// <summary>Esc：暂停菜单。</summary>
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape);

        /// <summary>B：营业前重新打开进货界面。</summary>
        public static bool BuyMenuPressed => Input.GetKeyDown(KeyCode.B);

        /// <summary>鼠标左键按下（收银扫描、UI）。</summary>
        public static bool PrimaryPressed => Input.GetMouseButtonDown(0);

        public static bool PrimaryHeld => Input.GetMouseButton(0);
        public static bool PrimaryReleased => Input.GetMouseButtonUp(0);

        public static Vector3 PointerScreenPosition => Input.mousePosition;

        /// <summary>
        /// 数字键 1/2/3 —— 远征时释放三名员工的技能（设计文档 §3.2
        /// 「技能按钮 1～3 / PC 数字键」）。和 ChoiceHotkey 同键位，
        /// 但两者的读取场景互斥：有弹窗时远征不接受输入。
        /// </summary>
        public static int SkillHotkey => ChoiceHotkey;

        /// <summary>R：撤退 — 设计文档 §3.2「撤退：保留部分战利品并提前回店」。</summary>
        public static bool RetreatPressed => Input.GetKeyDown(KeyCode.R);

        /// <summary>Q：目标标记 — 设计文档 §3.2「目标标记：优先攻击指定敌人」。</summary>
        public static bool MarkTargetPressed => Input.GetKeyDown(KeyCode.Q);

        /// <summary>数字键 1/2/3 —— 事件弹窗的快捷选择。</summary>
        public static int ChoiceHotkey
        {
            get
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
                return -1;
            }
        }
    }
}
