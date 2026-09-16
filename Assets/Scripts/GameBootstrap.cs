using UnityEngine;

namespace Game
{
    /// <summary>
    /// 游戏启动引导：进入 Play 模式后自动初始化架构，无需在场景中挂对象。
    /// 后续迭代如需场景级启动对象（控制初始化时机/顺序），可改为 GameRoot MonoBehaviour。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // 显式初始化架构（幂等：重复调用不会重复 new）
            GameArchitecture.InitArchitecture();
            Debug.Log("[菌核狂潮] 架构启动完成");
        }
    }
}
