using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 액터의 전투(공격) 로직을 담당하는 POCO 클래스입니다.
    /// 공격 쿨타임 관리 및 공격 명령 수행을 담당합니다.
    /// </summary>
    public class CombatLogic
{
        private readonly ICombatActor actor;
        private readonly float attackCooldown;
        private float lastAttackTime;

        /// <summary>
        /// 생성자에서 전투를 수행할 액터를 캐싱합니다.
        /// </summary>
        /// <param name="actor">전투 액터 인터페이스</param>
        /// <param name="attackSpeed">초당 공격 횟수</param>
        public CombatLogic(ICombatActor actor, float attackSpeed)
        {
            this.actor = actor;
            attackCooldown = 1f / Mathf.Max(0.1f, attackSpeed);
        }

        /// <summary>
        /// 매 프레임 업데이트 루프에서 호출되어 공격 가능 여부를 판단하고 공격을 수행합니다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!actor.IsAttackableState()) return;

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                ExecuteAttack();
            }
        }

        private void ExecuteAttack()
        {
            lastAttackTime = Time.time;
            actor.PerformAttack();
        }
    }
}
