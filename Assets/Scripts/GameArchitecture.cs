using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 菌核狂潮 全局架构单例：QFramework Architecture&lt;T&gt; 的唯一入口。
    /// 所有 System / Model / Utility 统一在 Init() 里注册。
    /// 首次访问 Interface（或调用 InitArchitecture）时触发懒初始化。
    /// </summary>
    public class GameArchitecture : Architecture<GameArchitecture>
    {
        protected override void Init()
        {
            Debug.Log("[菌核狂潮] GameArchitecture 初始化完成");

            // Utility（基础设施）
            RegisterUtility<IPoolUtility>(new PoolUtility());

            // Model（共享数据）
            RegisterModel<IGameStateModel>(new GameStateModel());

            // System（业务逻辑）
            RegisterSystem<IUpgradeSystem>(new UpgradeSystem());
            RegisterSystem<IDifficultySystem>(new DifficultySystem());

            // 玩家局内数值的唯一权威（由 角色 × 武器 派生 + 加成项）。
            // ⚠ 这是本工程第一个**带运行时状态**的 System —— 新增这类 System 时，
            //   必须在 StartNewGameCommand 里补一步复位，否则重开会残留上一局的数值。
            //   当前的复位步骤数见该命令内的编号清单。
            RegisterSystem<IPlayerStatSystem>(new PlayerStatSystem());
        }
    }
}
