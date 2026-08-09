// 캐릭터 타워 연출 — 공격 애니 오버라이드·속도 동기화(IAttackMotion). 무엇을 겨눌지·언제 죽을지는 타워가 지시한다
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Visual
{
    /// <summary>
    /// 캐릭터 타워의 연출 컴포넌트입니다. 공격 모션을 AnimatorOverrideController로 갈아끼우고
    /// 발사 주기에 맞춰 재생 속도를 맞춥니다(IAttackMotion). 상태 판단은 하지 않고 타워의 지시만 따릅니다.
    /// </summary>
    public sealed class CharacterVisual : MonoBehaviour, IAttackMotion
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform firePoint;   // 발사체·머즐 스폰 위치(레일건 총구 본)
        [SerializeField] private float rotateSpeed = 8f;

        /// <summary> 교체 대상이 되는 원본 공격 클립. 에디터가 컨트롤러의 Attack 상태에서 자동으로 채운다. </summary>
        [SerializeField] private AnimationClip baseAttackClip;

        /// <summary> 발사체·머즐 VFX가 나올 총구 Transform(합성 루트가 능력 시스템에 배선). </summary>
        public Transform FirePoint => firePoint;

        private const string ATTACK_STATE_NAME = "Attack";

        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
        private static readonly int LowHpHash = Animator.StringToHash("LowHP");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int VictoryHash = Animator.StringToHash("Victory");

        /// <summary> 공격 모션이 발사 프레임에 닿았을 때 발생합니다. 타워가 구독해 능력을 발사시킵니다. </summary>
        public event System.Action OnFireFrameReached;

        /// <summary> 타워가 지정한 조준 대상. 공격 중이 아닐 때 이쪽을 본다. </summary>
        private ITargetable aimTarget;

        private UnityEngine.Camera viewCamera;
        private AnimatorOverrideController overrideController;
        private bool locked;              // 파괴/승리 시 회전·공격 잠금
        private ITargetable castTarget;   // 발사 대상(조준 유지용)

        /// <summary> 연출을 재생 가능한 상태로 만듭니다(클립 교체용 오버라이드 컨트롤러 구성). </summary>
        public void Prepare()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            viewCamera = UnityEngine.Camera.main;

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
        }

        /// <summary> 겨눌 대상을 지정합니다. 타워가 자기 타겟을 알려줍니다. </summary>
        /// <param name="target">조준 대상(없으면 null)</param>
        public void SetAimTarget(ITargetable target)
        {
            aimTarget = target;
        }

        /// <summary> 코어가 위태로운 상태인지 알립니다. </summary>
        /// <param name="isLow">위태로우면 true</param>
        public void SetLowHp(bool isLow)
        {
            if (animator == null)
                return;

            animator.SetBool(LowHpHash, isLow);
        }

        /// <summary> 파괴 연출을 재생하고 이후 움직임을 멈춥니다. </summary>
        public void PlayDeath()
        {
            locked = true;
            if (animator != null) animator.SetTrigger(DeathHash);
        }

        /// <summary> 승리 연출을 재생하고 이후 움직임을 멈춥니다. </summary>
        public void PlayVictory()
        {
            locked = true;
            if (animator != null) animator.SetTrigger(VictoryHash);
        }

        private void Update()
        {
            if (!locked) FaceTarget();
        }

        /// <summary> 지정된 대상(없으면 카메라)을 향해 Y축으로 부드럽게 회전합니다. </summary>
        private void FaceTarget()
        {
            ITargetable target = (castTarget != null && castTarget.IsActive) ? castTarget : aimTarget;

            Vector3 dir;
            if (target != null && target.IsActive) dir = target.Position - transform.position;
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
            if (clip != null && overrideController != null && baseAttackClip != null)
                overrideController[baseAttackClip] = clip;

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
            OnFireFrameReached?.Invoke();
        }

#if UNITY_EDITOR
        /// <summary> 컨트롤러의 Attack 상태에 꽂힌 원본 클립을 에디터에서 자동으로 채웁니다. </summary>
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AnimationClip found = FindBaseAttackClip();
            if (found == null || found == baseAttackClip)
                return;

            baseAttackClip = found;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary> 애니메이터 컨트롤러에서 Attack 상태의 클립을 찾습니다. 없으면 null. </summary>
        private AnimationClip FindBaseAttackClip()
        {
            if (animator == null)
                return null;

            UnityEditor.Animations.AnimatorController controller =
                animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (controller == null)
                return null;

            foreach (UnityEditor.Animations.AnimatorControllerLayer layer in controller.layers)
            {
                foreach (UnityEditor.Animations.ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != ATTACK_STATE_NAME)
                        continue;

                    return child.state.motion as AnimationClip;
                }
            }

            return null;
        }
#endif
    }
}
