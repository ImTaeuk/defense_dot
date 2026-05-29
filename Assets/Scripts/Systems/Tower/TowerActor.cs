using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 타워 액터 클래스입니다. 전투(공격) 로직을 포함합니다.
    /// </summary>
    public class TowerActor : ActorBase<TowerData>, ICombatActor
    {
        private CombatLogic combatLogic;
        private ITargetable currentTarget;

        #region ICombatActor Implementation
        public bool IsAttackableState()
        {
            return currentState == ActorState.Idle || currentState == ActorState.Attacking;
        }

        public void PerformAttack()
        {
            if (currentTarget == null || !currentTarget.IsActive)
            {
                SetState(ActorState.Idle);
                return;
            }

            SetState(ActorState.Attacking);
            Debug.Log($"{data.towerName} attacks {currentTarget} for {data.attackDamage} damage.");
            
            if (currentTarget is IDamageable damageable)
            {
                damageable.TakeDamage(data.attackDamage);
            }
        }

        public void UpdateCombat(float deltaTime)
        {
            combatLogic?.Tick(deltaTime);
        }
        #endregion

        private void Awake()
        {
            if (data != null)
            {
                combatLogic = new CombatLogic(this, data.attackSpeed);
            }
        }

        public override void Initialize(TowerData actorData)
        {
            base.Initialize(actorData);
            combatLogic = new CombatLogic(this, actorData.attackSpeed);
        }

        private void Update()
        {
            // 타겟이 있을 때만 전투 로직 수행
            if (currentTarget != null && currentTarget.IsActive)
            {
                UpdateCombat(Time.deltaTime);
            }
            else
            {
                // 타겟 탐색 로직 (Behavior Tree에서 수행하거나 직접 구현)
                SearchTarget();
            }
        }

        private void SearchTarget()
        {
            // 임시 타겟 탐색 로직 (추후 그리드/범위 기반으로 고도화 필요)
        }

        public void SetTarget(ITargetable target)
        {
            currentTarget = target;
        }
    }
}
