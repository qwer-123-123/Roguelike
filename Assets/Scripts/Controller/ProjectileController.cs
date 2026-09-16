using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 投射物（表现层 MonoBehaviour，IController）：追踪目标敌人，命中后造成伤害并回收。
    /// 伤害由发射方在 Init 时注入（受升级加成）。
    /// </summary>
    public class ProjectileController : MonoBehaviour, IController
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float hitRadius = 0.5f;

        private EnemyController target;
        private int damage;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        public void Init(EnemyController target, int damage)
        {
            this.target = target;
            this.damage = damage;
        }

        private void Update()
        {
            // 阶段门禁：非局内阶段投射物静止在空中（死亡后世界冻结）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            // 目标死亡（被回收）则自身也回收
            if (target == null || !target.isActiveAndEnabled)
            {
                this.GetUtility<IPoolUtility>().Despawn(gameObject);
                return;
            }

            Vector3 dir = (target.transform.position - transform.position).normalized;
            transform.Translate(dir * (moveSpeed * Time.deltaTime));

            if ((target.transform.position - transform.position).sqrMagnitude < hitRadius * hitRadius)
            {
                target.TakeDamage(damage);
                this.GetUtility<IPoolUtility>().Despawn(gameObject);
            }
        }
    }
}
