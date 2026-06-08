using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary>
    /// IActor 의 StateChanged 를 구독해 상태별 스프라이트 클립을 재생합니다.
    /// 경량 프레임 플레이어(Animator 미사용, 풀링 친화).
    /// </summary>
    public sealed class SpriteActorView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteAnimationSet animationSet;

        private IActor actor;
        private SpriteAnimationSet.StateClip currentClip;
        private bool hasClip;
        private float elapsed;

        private void Awake()
        {
            actor = GetComponentInParent<IActor>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (actor == null) actor = GetComponentInParent<IActor>();
            if (actor != null)
            {
                actor.StateChanged += HandleStateChanged;
                ApplyState(actor.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (actor != null) actor.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(ActorState newState)
        {
            ApplyState(newState);
        }

        private void ApplyState(ActorState state)
        {
            if (animationSet == null) return;
            currentClip = animationSet.GetClip(state, out hasClip);
            elapsed = 0f;
            UpdateFrame();
        }

        private void Update()
        {
            if (!hasClip) return;
            elapsed += Time.deltaTime;
            UpdateFrame();
        }

        private void UpdateFrame()
        {
            if (!hasClip || spriteRenderer == null) return;
            Sprite[] frames = currentClip.frames;
            if (frames == null || frames.Length == 0) return;
            int idx = SpriteFrameMath.FrameIndex(elapsed, animationSet.framesPerSecond, frames.Length, currentClip.loop);
            spriteRenderer.sprite = frames[idx];
        }
    }
}
