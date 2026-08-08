// 타워 액터 — 사거리 내 타겟 탐색·공격, 풀링 대상
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Core.Pooling;
using DefenseDot.Data;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Loading;
using DefenseDot.Systems.Visual;

namespace DefenseDot.Systems.Tower
{
    /// <summary>
    /// 타워 액터 클래스입니다. 전투(공격) 로직과 사거리 기반 타겟 탐색을 포함하며,
    /// 자신이 가진 능력의 구동과 수명을 함께 책임집니다.
    /// </summary>
    public class TowerActor : ActorBase<TowerData>, IPoolable, ISceneWarmup
    {
        /// <summary> 이 타워의 3D 연출(조준·모션). 하위 오브젝트를 인스펙터로 연결한다. </summary>
        [SerializeField] private CharacterVisual visual;

        /// <summary> 이 타워가 가진 능력의 구동계. 타워가 소유하고 수명을 함께 한다. </summary>
        private readonly TowerAbilitySystem abilities = new TowerAbilitySystem();

        /// <summary> 게임 단계. 플레이 중이 아니면 능력을 돌리지 않는다. </summary>
        private GameFlowModel flow;

        private ITargetable currentTarget;
        private TargetFinder targetFinder;

        /// <summary> 이 타워의 연출입니다. 연출 없는 타워면 null 입니다. </summary>
        public CharacterVisual Visual => visual;

        /// <summary> 능력 명령 대상입니다. 카드 선택·강화가 씁니다. </summary>
        public IAbilityCommandTarget Abilities => abilities;

        /// <summary>
        /// 타겟 탐색기를 주입합니다. (배치 시 호출)
        /// </summary>
        public void SetTargetFinder(TargetFinder finder) => targetFinder = finder;

        /// <summary> 합성 루트가 능력 구동에 필요한 것을 주입합니다. </summary>
        /// <param name="finder">사거리 안의 적을 찾는 탐색기</param>
        /// <param name="combatState">로드아웃 수정자가 참조할 전투 상태</param>
        /// <param name="starters">타워 기본 공격 뒤에 붙일 스타터 능력</param>
        /// <param name="pool">이펙트 예열·스폰에 쓰는 풀</param>
        /// <param name="gameFlow">게임 단계. 플레이 중이 아니면 능력을 멈춘다</param>
        public void SetupAbilities(TargetFinder finder, ICombatState combatState,
            IReadOnlyList<AbilityData> starters, PoolSystem pool, GameFlowModel gameFlow)
        {
            if (data == null)
                return;

            targetFinder = finder;
            flow = gameFlow;

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

            // 3. 발사점은 연출의 총구를, 사거리는 타워 값을 쓴다(능력이 각자 값을 쓰지 않게)
            Transform fireOrigin = visual != null ? visual.FirePoint : null;
            abilities.Setup(finder, Position, combatState, combined, pool, fireOrigin, data.attackRange);

            // 4. 예열은 로딩이 기다린다. 로더 없는 씬 단독 실행이면 스스로 수행
            if (SceneLoadManager.Instance != null)
                SceneLoadManager.Instance.RegisterWarmup(this);
            else
                WarmupAsync(destroyCancellationToken).Forget();
        }

        /// <summary> 자기 능력을 한 프레임 진행시킵니다. 플레이 중이 아니면 멈춥니다. </summary>
        private void Update()
        {
            if (flow != null && !flow.IsPlaying)
                return;

            abilities.Tick(Time.deltaTime);
        }

        /// <summary> 기본 공격 속도를 바꿉니다. </summary>
        /// <param name="attacksPerSecond">초당 공격 횟수</param>
        public void SetAttackSpeed(float attacksPerSecond)
        {
            abilities.SetBaseAttackSpeed(attacksPerSecond);
        }

        /// <summary> 기본 공격을 1회 시도합니다(브레인이 주기를 보고 부릅니다). </summary>
        /// <returns>발사했으면 다음 발사까지의 간격(초), 못 쐈으면 0</returns>
        public float TryFireBasic()
        {
            return abilities.TryFireBasic();
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
            ReleaseAbilities();   // 재사용 시 이전 판의 로드아웃·구독이 따라오지 않게
        }
        #endregion

        /// <summary> 능력 구동계를 정리합니다. </summary>
        private void OnDestroy()
        {
            ReleaseAbilities();
        }

        /// <summary> 연출 구독을 끊고 능력 구동계를 놓습니다. 파괴·풀 반환에서 함께 씁니다. </summary>
        private void ReleaseAbilities()
        {
            if (visual != null)
                visual.OnFireFrameReached -= HandleFireFrameReached;

            abilities.Dispose();
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
