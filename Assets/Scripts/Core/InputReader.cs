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

        /// <summary>Shift：加速移动。</summary>
        public static bool Sprint =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /// <summary>E：交互（按下瞬间）。</summary>
        public static bool InteractPressed => Input.GetKeyDown(KeyCode.E);

        /// <summary>E：长按交互（补货、扶货架、清理污渍）。</summary>
        public static bool InteractHeld => Input.GetKey(KeyCode.E);

        /// <summary>Tab：打开图鉴。</summary>
        public static bool BestiaryPressed => Input.GetKeyDown(KeyCode.Tab);

        /// <summary>Esc：暂停菜单。</summary>
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape);

        /// <summary>鼠标左键按下（收银扫描、UI）。</summary>
        public static bool PrimaryPressed => Input.GetMouseButtonDown(0);

        public static bool PrimaryHeld => Input.GetMouseButton(0);
        public static bool PrimaryReleased => Input.GetMouseButtonUp(0);

        public static Vector3 PointerScreenPosition => Input.mousePosition;

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
