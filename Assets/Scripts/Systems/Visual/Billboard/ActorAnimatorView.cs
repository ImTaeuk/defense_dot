// 액터 이동을 Animator(IsMoving/Direction) 파라미터로 브리지
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary>
    /// 액터의 위치 변화를 Cainos AC Player Animator 파라미터로 변환합니다.
    /// 이동 여부 → IsMoving, 이동 방향 → Direction(카메라 기준 4방향).
    /// (속도 기반 — 액터 상태 전환에 의존하지 않음)
    /// </summary>
    public sealed class ActorAnimatorView : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private float moveThreshold = 0.01f;

        private static readonly int directionHash = Animator.StringToHash("Direction");
        private static readonly int isMovingHash = Animator.StringToHash("IsMoving");

        private IActor actor;
        private Vector3 lastPosition;

        private void Awake()
        {
            actor = GetComponentInParent<IActor>();
            if (animator == null) animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            if (actor == null) actor = GetComponentInParent<IActor>();
            if (actor != null) lastPosition = actor.Position;
        }

        private void Update()
        {
            if (actor == null || animator == null) return;

            Vector3 pos = actor.Position;
            Vector3 delta = pos - lastPosition;
            lastPosition = pos;
            delta.y = 0f;

            bool moving = delta.sqrMagnitude > moveThreshold * moveThreshold;
            animator.SetBool(isMovingHash, moving);
            if (moving)
            {
                animator.SetInteger(directionHash, BillboardMath.DirectionIndex(delta, ResolveCameraYaw()));
            }
        }

        private float ResolveCameraYaw()
        {
            UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
            return cam != null ? cam.transform.eulerAngles.y : 0f;
        }
    }
}
