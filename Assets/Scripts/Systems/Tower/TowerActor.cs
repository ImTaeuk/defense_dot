// 타워 액터 — 사거리 내 타겟 탐색·공격, 풀링 대상
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 타워 액터 클래스입니다. 전투(공격) 로직과 사거리 기반 타겟 탐색을 포함합니다.
    /// </summary>
    public class TowerActor : ActorBase<TowerData>, ICombatActor, IPoolable
    {
        private CombatLogic combatLogic;
        private ITargetable currentTarget;
        private TargetFinder targetFinder;

        /// <summary>
        /// 타겟 탐색기를 주입합니다. (배치 시 호출)
        /// </summary>
        public void SetTargetFinder(TargetFinder finder) => targetFinder = finder;

        #region IPoolable Implementation
        public void OnSpawn()
        {
            currentTarget = null;
            SetState(ActorState.Idle);
        }

        public void OnDespawn()
        {
            currentTarget = null;
            SetState(ActorState.Idle);
        }
        #endregion

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
            // 타겟이 유효(생존 + 사거리 내)하면 공격, 아니면 재탐색
            if (IsTargetValid())
            {
                UpdateCombat(Time.deltaTime);
            }
            else
            {
                currentTarget = null;
                SearchTarget();
            }
        }

        private bool IsTargetValid()
        {
            if (currentTarget == null || !currentTarget.IsActive || data == null) return false;
            float rangeSqr = data.attackRange * data.attackRange;
            return (currentTarget.Position - Position).sqrMagnitude <= rangeSqr;
        }

        private void SearchTarget()
        {
            if (targetFinder == null || data == null) return;
            ITargetable target = targetFinder.FindNearest(Position, data.attackRange);
            if (target != null) SetTarget(target);
        }

        public void SetTarget(ITargetable target)
        {
            currentTarget = target;
        }
    }
}
