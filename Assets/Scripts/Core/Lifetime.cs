using UnityEngine;

namespace MonsterMart.Core
{
    /// <summary>
    /// 销毁运行时对象。
    ///
    /// Object.Destroy 在编辑器的非播放模式下是非法的（会报
    /// 「Destroy may not be called from edit mode」），而
    /// Assets/Editor/SmokeTests 正是在那个模式下把运行时对象装配起来跑无头用例。
    /// 这里按模式分流，业务代码不必关心自己跑在哪。
    /// </summary>
    public static class Lifetime
    {
        public static void Destroy(Object target, float delaySeconds = 0f)
        {
            if (target == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // 编辑器下没有帧循环，延迟销毁没有意义，直接拆掉
                Object.DestroyImmediate(target);
                return;
            }
#endif
            if (delaySeconds > 0f) Object.Destroy(target, delaySeconds);
            else Object.Destroy(target);
        }
    }
}
