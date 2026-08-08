// 타워 액터 — 사거리 내 타겟 탐색·공격, 풀링 대상
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Core.Pooling;
using DefenseDot.Data;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Loading;
using DefenseDot.Systems.Tower.Debugging;
using DefenseDot.Systems.Visual;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 타워 액터 클래스입니다. 전투(공격) 로직과 사거리 기반 타겟 탐색을 포함하며,
    /// 자신이 가진 능력의 구동과 수명을 함께 책임집니다.
    /// </summary>
    public class TowerActor : ActorBase<TowerData>, ICombatActor, IPoolable, ISceneWarmup
    {
        /// <summary> 이 타워의 3D 연출(조준·모션). 하위 오브젝트를 인스펙터로 연결한다. </summary>
        [SerializeField] private CharacterVisual visual;

        /// <summary> 이 타워가 가진 능력의 구동계. 타워가 소유하고 수명을 함께 한다. </summary>
        private readonly TowerAbilitySystem abilities = new TowerAbilitySystem();

        private CombatLogic combatLogic;
        private ITargetable currentTarget;
        private TargetFinder targetFinder;

        /// <summary> 이 타워의 연출입니다. 연출 없는 타워면 null 입니다. </summary>
        public CharacterVisual Visual => visual;

        /// <summary> 능력 명령 대상입니다. 카드 선택·강화가 씁니다. </summary>
        public IAbilityCommandTarget Abilities => abilities;

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

        /// <summary> 합성 루트가 능력 구동에 필요한 것을 주입합니다. </summary>
        /// <param name="finder">사거리 안의 적을 찾는 탐색기</param>
        /// <param name="combatState">로드아웃 수정자가 참조할 전투 상태</param>
        /// <param name="starters">타워 기본 공격 뒤에 붙일 스타터 능력</param>
        /// <param name="pool">이펙트 예열·스폰에 쓰는 풀</param>
        public void SetupAbilities(TargetFinder finder, ICombatState combatState,
            IReadOnlyList<AbilityData> starters, PoolSystem pool)
        {
            if (data == null)
                return;

            targetFinder = finder;

            // 1. 모션·클립·공격속도를 먼저 주입해야 무기가 올바른 값으로 생성된다
            if (visual != null)
            {
                abilities.SetAttackMotion(visual);
                visual.OnFireFrameReached += HandleFireFrameReached;
            }

            abilities.SetCastAnimation(data.castAnimation);
            abilities.SetBaseAttackSpeed(data.attackSpeed);

            // 2. 타워 기본 공격을 스타터 맨 앞에 합성(중복은 로드아웃이 방어)
            List<AbilityData> combined = new List<AbilityData>();
            if (data.basicAttack != null)
                combined.Add(data.basicAttack);

            if (starters != null)
                combined.AddRange(starters);

            // 3. 발사점은 연출의 총구를 쓴다(없으면 타워 위치로 폴백)
            Transform fireOrigin = visual != null ? visual.FirePoint : null;
            abilities.Setup(finder, Position, combatState, combined, pool, fireOrigin);

            // 4. 예열은 로딩이 기다린다. 로더 없는 씬 단독 실행이면 스스로 수행
            if (SceneLoadManager.Instance != null)
                SceneLoadManager.Instance.RegisterWarmup(this);
            else
                WarmupAsync(destroyCancellationToken).Forget();
        }

        /// <summary> 능력 구동계를 한 프레임 진행시킵니다. </summary>
        /// <param name="deltaTime">경과 시간</param>
        public void TickAbilities(float deltaTime)
        {
            abilities.Tick(deltaTime);
        }

        /// <summary> 기본 공격 속도를 바꿉니다. </summary>
        /// <param name="attacksPerSecond">초당 공격 횟수</param>
        public void SetAttackSpeed(float attacksPerSecond)
        {
            abilities.SetBaseAttackSpeed(attacksPerSecond);
        }

        /// <summary> 능력을 사용 가능한 상태로 만듭니다(예열 후 장착). </summary>
        /// <param name="cancellationToken">씬 파괴 등으로 중단할 때 쓰는 토큰</param>
        public async UniTask WarmupAsync(System.Threading.CancellationToken cancellationToken)
        {
            bool canceled = await abilities.WarmupStartersAsync()
                .AttachExternalCancellation(cancellationToken)
                .SuppressCancellationThrow();
            if (canceled)
                return;

            abilities.EquipAll();   // 예열 성패 무관하게 장착
        }

        /// <summary> 연출의 발사 프레임을 능력 구동계에 전달합니다. </summary>
        private void HandleFireFrameReached()
        {
            abilities.NotifyFireFrame();
        }

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
            if (currentTarget == null || !currentTarget.IsActive) return;   // 상태는 브레인이 기록

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

        /// <summary> 연출 구독을 끊고 능력 구동계를 정리합니다. </summary>
        private void OnDestroy()
        {
            if (visual != null)
                visual.OnFireFrameReached -= HandleFireFrameReached;

            abilities.Dispose();
        }

        public override void Initialize(TowerData actorData)
        {
            base.Initialize(actorData);
            combatLogic = new CombatLogic(this, actorData.attackSpeed);
        }

        /// <summary> 현재 타겟이 유효(생존+사거리)한지(브레인 조건). </summary>
        public bool HasValidTarget() => IsTargetValid();

        /// <summary> 사거리 내 타겟을 탐색·설정(브레인 액션). </summary>
        public void AcquireTarget() => SearchTarget();

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
