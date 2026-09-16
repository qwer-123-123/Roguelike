using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 经验掉落物（表现层 MonoBehaviour，IController）：玩家靠近后拾取，加经验并回收。
    /// </summary>
    public class ExperienceDropController : MonoBehaviour, IController
    {
        [SerializeField] private float pickupRadius = 0.8f;
        [SerializeField] private int expAmount = 1;

        private Transform player;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        public void Init(Transform player)
        {
            this.player = player;
        }

        private void Update()
        {
            if (player == null) return;
            // 阶段门禁：非局内阶段不拾取（死亡后世界冻结，掉落物静止）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            if ((player.position - transform.position).sqrMagnitude < pickupRadius * pickupRadius)
            {
                PickUp();
            }
        }

        private void PickUp()
        {
            this.SendCommand(new AddExperienceCommand(expAmount));
            this.GetUtility<IPoolUtility>().Despawn(gameObject);
        }
    }
}
