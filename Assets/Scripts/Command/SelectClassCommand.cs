using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 选择兵种（无状态命令）。
    /// 兵种选择界面只发本命令，不直接写 Model —— 与「状态变更必须通过明确的规则系统」一致。
    ///
    /// 写入 <see cref="IGameStateModel.SelectedClassIndex"/> 会触发
    /// <see cref="PlayerStatSystem"/> 重建基准（它注册了该属性的监听）。
    /// </summary>
    public class SelectClassCommand : AbstractCommand
    {
        private readonly int classIndex;

        public SelectClassCommand(int classIndex)
        {
            this.classIndex = classIndex;
        }

        protected override void OnExecute()
        {
            var catalog = Object.FindObjectOfType<GameConfigHolder>()?.Catalog;
            int count = catalog != null ? catalog.Count : 0;

            if (count <= 0)
            {
                Debug.LogWarning("[菌核狂潮] SelectClassCommand：配置表未绑定或兵种数为 0，忽略本次选择");
                return;
            }

            var clamped = Mathf.Clamp(classIndex, 0, count - 1);
            if (clamped != classIndex)
            {
                Debug.LogWarning($"[菌核狂潮] SelectClassCommand：索引 {classIndex} 越界（共 {count} 个兵种），已夹到 {clamped}");
            }

            this.GetModel<IGameStateModel>().SelectedClassIndex.Value = clamped;
        }
    }
}
