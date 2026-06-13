using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary>
    /// ActorState 변화를 Animator 파라미터로 번역해 푸시하는 공용 바인더입니다.
    /// State(int)=(int)ActorState, 이동 중에는 Direction(int)을 푸시합니다.
    /// 전환 규칙은 AnimatorController 에셋이 소유 — 프리팹별 컨트롤러 교체가 곧 오버라이드입니다.
    /// </summary>
    public class ActorAnimatorBinder : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private float moveThreshold = 0.01f;

        private static readonly int stateHash = Animator.StringToHash("State");
        private static readonly int directionHash = Animator.StringToHash("Direction");

        private IActor actor;
        private Vector3 lastPosition;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            actor = GetComponentInParent<IActor>();
        }

        private void OnEnable()
        {
            if (actor == null) actor = GetComponentInParent<IActor>();
            if (actor == null) return;
            lastPosition = actor.Position;
            actor.StateChanged += HandleStateChanged;
            HandleStateChanged(actor.CurrentState);
        }

        private void OnDisable()
        {
            if (actor != null) actor.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(ActorState state)
        {
            if (animator != null) animator.SetInteger(stateHash, (int)state);
        }

        private void LateUpdate()
        {
            if (actor == null || animator == null) return;
            if (actor.CurrentState != ActorState.Moving)
            {
                lastPosition = actor.Position;
                return;
            }
            Vector3 pos = actor.Position;
            Vector3 delta = pos - lastPosition;
            lastPosition = pos;
            delta.y = 0f;
            if (delta.sqrMagnitude < moveThreshold * moveThreshold) return;
            animator.SetInteger(directionHash, ResolveDirection(delta));
        }

        /// <summary> 이동 델타 → 카메라 기준 4방향 인덱스. 특수 액터는 오버라이드. </summary>
        protected virtual int ResolveDirection(Vector3 worldDelta)
        {
            UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            return BillboardMath.DirectionIndex(worldDelta, yaw);
        }
    }
}
