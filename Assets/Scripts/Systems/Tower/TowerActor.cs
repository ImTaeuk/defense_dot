// 타워 액터 — 사거리 내 타겟 탐색·공격, 풀링 대상
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Tower.Debugging;

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

        // DEBUG: 공격 타입 테스트 — 실제 능력 시스템 구현 시 삭제
        [Header("DEBUG Attack Toggles")]
        [SerializeField] private bool debugSingle = true;
        [SerializeField] private bool debugAoe = false;
        [SerializeField] private bool debugProjectile = false;

        private readonly IAttackBehavior singleBehavior = new SingleTargetAttack();
        private readonly IAttackBehavior aoeBehavior = new AoeAttack();
        private readonly IAttackBehavior projectileBehavior = new ProjectileAttack();
        private readonly List<IAttackBehavior> activeBehaviors = new List<IAttackBehavior>();

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

            // DEBUG: 토글에서 활성 behavior 구성 후 순회 실행
            activeBehaviors.Clear();
            if (debugSingle) activeBehaviors.Add(singleBehavior);
            if (debugAoe) activeBehaviors.Add(aoeBehavior);
            if (debugProjectile) activeBehaviors.Add(projectileBehavior);

            if (activeBehaviors.Count == 0) return;
            AttackContext ctx = new AttackContext(this, Position, targetFinder, data);
            for (int i = 0; i < activeBehaviors.Count; i++) activeBehaviors[i].Execute(in ctx);
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

        // DEBUG: 공격 범위 기즈모 — 실제 능력 시스템 구현 시 삭제
        private void OnDrawGizmos()
        {
            if (data == null) return;
            Gizmos.color = new Color(1f, 0.82f, 0.2f, 0.5f);
            float r = data.attackRange;
            Vector3 c = transform.position;
            Vector3 prev = c + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
