using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 游戏流程控制器（表现层 MonoBehaviour，IController）：驱动存活时间。
    /// 只在 Playing 阶段累加，开始界面与死亡后都不计时。
    /// </summary>
    public class GameFlowController : MonoBehaviour, IController
    {
        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private void Update()
        {
            var model = this.GetModel<IGameStateModel>();
            if (model.Phase.Value != GamePhase.Playing) return;
            model.SurvivedTime.Value += Time.deltaTime;
        }
    }
}
