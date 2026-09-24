using QFramework;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 玩家（表现层 MonoBehaviour）：移动、血量、受击，以及角色表现状态的调度。
    ///
    /// 数值全部**每帧从 <see cref="IPlayerStatSystem"/> 拉取**，本类不再持有可变量。
    /// 这是本次改造最省事的一刀：原先 <c>moveSpeed</c> / <c>maxHealth</c> 是本地字段、
    /// 升级走「推」接口去改它们，于是重开必须逐项复位，漏一项就残留上一局的数值。
    /// 收敛之后，重开只需 PlayerStatSystem.ResetStats() 一次。
    /// </summary>
    public class PlayerController : MonoBehaviour, IController
    {
        [SerializeField] private CharacterView view;

        private float currentHp;
        private bool dead;

        public IArchitecture GetArchitecture() => GameArchitecture.Interface;

        private IPlayerStatSystem Stats => this.GetSystem<IPlayerStatSystem>();

        // HUD 仍按整数显示，保持既有调用点不变
        public int MaxHealth => Mathf.CeilToInt(Stats.MaxHp);
        public int CurrentHealth => Mathf.CeilToInt(Mathf.Max(0f, currentHp));

        private void Start()
        {
            ResetForNewGame();
        }

        /// <summary>重开一局：血量回满、位置归原点、表现复位。</summary>
        public void ResetForNewGame()
        {
            dead = false;
            currentHp = Stats.MaxHp;
            gameObject.SetActive(true);
            transform.position = Vector3.zero;
            view?.ResetView();
            ApplyCharacterSprites();
        }

        /// <summary>把当前兵种的动画集绑给表现组件。</summary>
        public void ApplyCharacterSprites()
        {
            if (view == null) return;
            view.Configure(Stats.Sprites);
        }

        private void Update()
        {
            // 阶段门禁：开始界面未开始、或玩家已死亡时不可移动（世界冻结）
            if (this.GetModel<IGameStateModel>().Phase.Value != GamePhase.Playing) return;

            if (dead)
            {
                view?.Play(AnimState.Death);
                return;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 moveDir = new Vector2(h, v);
            if (moveDir.sqrMagnitude > 1f)
            {
                moveDir.Normalize();
            }

            transform.Translate(moveDir * (Stats.Speed * Time.deltaTime));

            // 表现状态：移动时走，静止时待机。
            //
            // 攻击动画由 PlayerCombatController 抢占，播完后要**由这里负责切回去**。
            // 判据必须带 `IsFinished`：只判「状态不是 Attack」是错的 —— 动画播完后
            // `state` 仍停在 Attack（非循环 clip 播完只置 IsFinished，不改 state），
            // 于是这个分支永远进不去，角色僵死在攻击帧上。
            // 症状：附近没敌人时不再触发新攻击，攻击帧就彻底卡住不动。
            if (view != null)
            {
                bool attackPlaying = view.State == AnimState.Attack && !view.IsFinished;
                if (!attackPlaying)
                {
                    view.Play(moveDir.sqrMagnitude > 0.0001f ? AnimState.Walk : AnimState.Idle);
                }
            }
        }

        /// <summary>设置朝向（弧度）。由攻击控制器在瞄准时调用。</summary>
        public void SetAim(float radians)
        {
            view?.SetAim(radians);
        }

        /// <summary>播放攻击动画。播完自动回到 Idle/Walk。</summary>
        public void PlayAttackAnim()
        {
            view?.Play(AnimState.Attack, restart: true);
        }

        /// <summary>攻击动画是否已播完（攻击控制器据此回到待机）。</summary>
        public bool IsAttackAnimFinished => view == null || view.State != AnimState.Attack || view.IsFinished;

        /// <summary>攻击动画的归一化进度 0~1。攻击控制器据此判断打击帧是否已到。</summary>
        public float AttackAnimProgress => view != null ? view.NormalizedTime : 1f;

        public void TakeDamage(float damage)
        {
            if (dead) return;

            // 减伤：常驻护甲 + 限时护甲（已由系统封顶 75%）
            float armor = Stats.EffectiveArmor;
            float real = Mathf.Max(1f, damage * (1f - armor));
            currentHp -= real;

            view?.Flash(1f);

            if (currentHp <= 0f)
            {
                currentHp = 0f;
                Die();
            }
        }

        /// <summary>反弹伤害（荆棘升级）。</summary>
        public float Thorns => Stats.Thorns;

        private void Die()
        {
            dead = true;
            view?.Play(AnimState.Death);
            this.SendCommand(new GameOverCommand());
        }
    }
}
