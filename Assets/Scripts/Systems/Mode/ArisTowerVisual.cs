// Aris 코어 타워 연출 — 공격 애니 오버라이드·속도 동기화(IAttackMotion) + 코어 상태·적 방향 연동
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Domain;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// Aris 코어 타워의 연출 컴포넌트입니다. 공격 모션을 AnimatorOverrideController로 갈아끼우고
    /// 발사 주기에 맞춰 재생 속도를 맞추며(IAttackMotion), 코어 HP·게임 단계·적 방향을 Animator·회전에 연동합니다.
    /// </summary>
    public sealed class ArisTowerVisual : MonoBehaviour, IAttackMotion
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform firePoint;   // 발사체·머즐 스폰 위치(레일건 총구 본)
        [SerializeField] private float rotateSpeed = 8f;
        [SerializeField] private float targetRange = 30f;
        [SerializeField] private float lowHpRatio = 0.3f;

        /// <summary> 발사체·머즐 VFX가 나올 총구 Transform(합성 루트가 능력 시스템에 배선). </summary>
        public Transform FirePoint => firePoint;

        private const string AttackClipKey = "Aris_Original_Normal_Attack_Ing";
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
        private static readonly int LowHpHash = Animator.StringToHash("LowHP");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int VictoryHash = Animator.StringToHash("Victory");

        private TowerAbilitySystem abilitySystem;
        private TargetFinder finder;
        private GameFlowModel flow;
        private CoreModel coreHp;
        private Camera viewCamera;
        private System.IDisposable healthSub;
        private AnimatorOverrideController overrideController;
        private bool locked;              // 파괴/승리 시 회전·공격 잠금
        private ITargetable castTarget;   // 발사 대상(조준 유지용)

        /// <summary> 합성 루트가 의존성을 주입하고 이벤트를 연결합니다. </summary>
        public void Setup(TowerAbilitySystem towerAbility, TargetFinder targetFinder,
            GameFlowModel gameFlow, CoreModel coreModel)
        {
            Unsubscribe();
            abilitySystem = towerAbility;
            finder = targetFinder;
            flow = gameFlow;
            coreHp = coreModel;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            viewCamera = Camera.main;

            // 시전 클립 교체용 오버라이드 컨트롤러 준비
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
                if (overrideController == null)
                {
                    overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
                    animator.runtimeAnimatorController = overrideController;
                }
            }

            if (coreHp != null)
            {
                healthSub = coreHp.Health.Subscribe(HandleHealthChanged);
                coreHp.OnCoreDestroyed += HandleCoreDestroyed;
            }
            if (flow != null) flow.OnPhaseChanged += HandlePhaseChanged;
        }

        private void Update()
        {
            if (!locked) FaceTarget();
        }

        /// <summary> 최근접 적(없으면 카메라)을 향해 Y축으로 부드럽게 회전합니다. </summary>
        private void FaceTarget()
        {
            Vector3 dir;
            ITargetable target = (castTarget != null && castTarget.IsActive)
                ? castTarget
                : (finder != null ? finder.FindNearest(transform.position, targetRange) : null);
            if (target != null) dir = target.Position - transform.position;
            else if (viewCamera != null) dir = viewCamera.transform.position - transform.position;
            else return;

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, rotateSpeed * Time.deltaTime);
        }

        #region IAttackMotion
        /// <summary> Attack 슬롯을 지정 클립으로 교체하고 지정 속도로 재생합니다. </summary>
        /// <param name="clip">재생할 공격 모션</param>
        /// <param name="target">조준 대상</param>
        /// <param name="speed">재생 속도 배수</param>
        public void PlayAttack(AnimationClip clip, ITargetable target, float speed)
        {
            if (locked || animator == null) return;

            castTarget = target;
            if (clip != null && overrideController != null) overrideController[AttackClipKey] = clip;
            animator.SetFloat(AttackSpeedHash, Mathf.Max(0.01f, speed));
            animator.SetTrigger(AttackHash);
        }
        #endregion

        /// <summary> 공격 모션의 발사 프레임에서 AnimationEvent가 호출합니다. </summary>
        public void OnFireFrame()
        {
            // 발사 순간 대상으로 정확히 정렬 → 보는 방향과 투사체 방향 일치
            if (castTarget != null && castTarget.IsActive)
            {
                Vector3 d = castTarget.Position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            if (abilitySystem != null) abilitySystem.NotifyFireFrame();
        }

        private void HandleHealthChanged(DefenseDot.Domain.Models.HealthState state)
        {
            if (animator == null) return;
            animator.SetBool(LowHpHash, state.Ratio <= lowHpRatio);
        }

        private void HandleCoreDestroyed()
        {
            locked = true;
            if (animator != null) animator.SetTrigger(DeathHash);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Victory) return;

            locked = true;
            if (animator != null) animator.SetTrigger(VictoryHash);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (coreHp != null)
            {
                healthSub?.Dispose();
                coreHp.OnCoreDestroyed -= HandleCoreDestroyed;
            }
            if (flow != null) flow.OnPhaseChanged -= HandlePhaseChanged;
        }
    }
}
