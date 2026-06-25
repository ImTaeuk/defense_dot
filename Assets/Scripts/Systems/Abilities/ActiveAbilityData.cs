using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 발동형 능력(추상). 매 프레임 Tick으로 구동합니다. </summary>
    public abstract class ActiveAbilityData : AbilityData
    {
        /// <summary> 기본 쿨다운(초). </summary>
        public float baseCooldown = 1f;

        /// <summary> 시전 애니메이션(선택). null이면 애니 없이 즉시 발사. </summary>
        [SerializeField] private AnimationClip castAnimation;

        /// <summary> 타겟 탐색 사거리. 서브클래스가 재정의. </summary>
        protected virtual float Range => 30f;

        /// <summary> 레벨별 효과값(데미지 등). 서브클래스가 재정의. </summary>
        public virtual float ValueAtLevel(int level) { return level; }

        /// <summary> 레벨별 쿨다운(보정 미적용). </summary>
        public virtual float CooldownAtLevel(int level) { return baseCooldown; }

        /// <summary> 매 프레임 구동 — 쿨다운·타겟 후 시전 요청 또는 즉시 발사. (상시 능력은 재정의로 무시) </summary>
        public virtual void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime)
        {
            if (!TickCooldown(self, deltaTime)) return;
            ITargetable target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, Range) : null;
            if (target == null) return;   // 준비 유지·재시도
            if (castAnimation != null && ctx.Cast != null)
            {
                if (!ctx.Cast.RequestCast(this, self, target, castAnimation)) return;   // 시전 중이면 대기
            }
            else
            {
                Fire(ctx, self, target);   // 애니 없으면 즉시
            }
            ResetCooldown(self, ctx);
        }

        /// <summary> 실제 발사(효과 생성)입니다. 서브클래스가 구현합니다. </summary>
        protected abstract void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target);

        /// <summary> 시전 호스트가 발사 프레임에 호출하는 래퍼입니다. </summary>
        internal void FireFromHost(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            Fire(ctx, self, target);
        }

        /// <summary> 쿨다운을 감소시키고 준비 여부를 반환합니다. (리셋 안 함) </summary>
        protected bool TickCooldown(AbilityInstance self, float deltaTime)
        {
            self.cooldownRemaining -= deltaTime;
            return self.cooldownRemaining <= 0f;
        }

        /// <summary> 발동 성공 후 쿨다운을 리셋합니다. (보정·하한 적용) </summary>
        protected void ResetCooldown(AbilityInstance self, in AbilityContext ctx)
        {
            self.cooldownRemaining = Mathf.Max(0.05f, CooldownAtLevel(self.level) - ctx.Modifiers.cooldownReduction);
        }
    }
}
