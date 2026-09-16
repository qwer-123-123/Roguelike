using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 难度系统（系统层 ISystem）：难度随时间递增的表现是刷怪间隔缩短，并推进波次计数。
    /// <see cref="GetSpawnInterval"/> 是刷怪间隔的**唯一来源**，刷怪器不得自备公式。
    /// </summary>
    public interface IDifficultySystem : ISystem
    {
        /// <summary>当前刷怪间隔（随存活时间线性缩短，不低于下限）。</summary>
        float GetSpawnInterval();
    }

    public class DifficultySystem : AbstractSystem, IDifficultySystem
    {
        private const float BaseSpawnInterval = 2f;
        private const float MinSpawnInterval = 0.4f;
        /// <summary>每存活多少秒刷怪间隔缩短 0.02s。</summary>
        private const float IntervalDecayPerSecond = 0.02f;
        private const int WaveIntervalSeconds = 30;

        protected override void OnInit()
        {
            var model = this.GetModel<IGameStateModel>();
            model.SurvivedTime.Register(_ => OnTimeChanged());
        }

        private void OnTimeChanged()
        {
            var model = this.GetModel<IGameStateModel>();
            int wave = 1 + (int)(model.SurvivedTime.Value / WaveIntervalSeconds);
            if (wave != model.Wave.Value)
            {
                model.Wave.Value = wave;
            }
        }

        public float GetSpawnInterval()
        {
            float t = this.GetModel<IGameStateModel>().SurvivedTime.Value;
            return Mathf.Max(MinSpawnInterval, BaseSpawnInterval - t * IntervalDecayPerSecond);
        }
    }
}
