using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 玩家（表现层 MonoBehaviour）：移动 + 血量 + 受击。
    /// </summary>
    public class PlayerController : MonoBehaviour, IController
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private int maxHealth = 10;

        private int currentHealth;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private void Start()
        {
            ResetForNewGame();
        }

        /// <summary>重开一局：血量回满、位置回原点、移速恢复默认、死亡标志清除。</summary>
        public void ResetForNewGame()
        {
            currentHealth = maxHealth;
            moveSpeed = 5f;
            gameObject.SetActive(true);
            transform.position = Vector3.zero;
        }

        private void Update()
        {
            // 阶段门禁：开始界面未开始、或玩家已死亡时不可移动（世界冻结）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 moveDir = new Vector2(h, v);
            if (moveDir.sqrMagnitude > 1f)
            {
                moveDir.Normalize();
            }

            transform.Translate(moveDir * (moveSpeed * Time.deltaTime));
        }

        public void TakeDamage(int damage)
        {
            if (currentHealth <= 0) return;
            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
        }

        private void Die()
        {
            this.SendCommand(new GameOverCommand());
        }

        // 升级接口
        public void AddMoveSpeed(float amount) => moveSpeed += amount;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
    }
}
