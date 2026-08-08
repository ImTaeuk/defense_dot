using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 발동형 능력(추상). 언제 발동하는지는 파생 타입(Main·Sub·Auto)이 정합니다. </summary>
    public abstract class ActiveAbilityData : AbilityData
    {
        /// <summary> 기본 쿨다운(초). Auto 계열만 사용합니다. </summary>
        public float baseCooldown = 1f;

        /// <summary> 타겟 탐색 사거리. 서브클래스가 재정의. </summary>
        protected virtual float Range => 30f;

        /// <summary> 타겟 탐색 사거리(외부 조회용). </summary>
        public float TargetRange => Range;

        /// <summary> 레벨별 효과값(데미지 등). 서브클래스가 재정의. </summary>
        public virtual float ValueAtLevel(int level) { return level; }

        /// <summary> 레벨별 쿨다운(보정 미적용). </summary>
        public virtual float CooldownAtLevel(int level) { return baseCooldown; }

        /// <summary> 실제 발사(효과 생성)입니다. 서브클래스가 구현합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        protected abstract void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target);

        /// <summary> 무기가 발사 프레임에 호출하는 래퍼입니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        internal void FireFromWeapon(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            Fire(ctx, self, target);
        }

        /// <summary> 쿨다운을 감소시키고 준비 여부를 반환합니다(리셋하지 않음). </summary>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="deltaTime">경과 시간(초)</param>
        protected bool TickCooldown(AbilityInstance self, float deltaTime)
        {
            self.cooldownRemaining -= deltaTime;
            return self.cooldownRemaining <= 0f;
        }

        /// <summary> 발동 성공 후 쿨다운을 재적재합니다(초과분 이월·배율·하한 적용). </summary>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        protected void ResetCooldown(AbilityInstance self, in AbilityContext ctx)
        {
            float rate = ctx.Stats != null ? ctx.Stats.cooldownRate : 1f;
            float cooldown = Mathf.Max(0.05f, CooldownAtLevel(self.level) * rate);
            self.cooldownRemaining += cooldown;
            if (self.cooldownRemaining < 0f)
            {
                self.cooldownRemaining = 0f;
            }
        }

        /// <summary> 자기 쿨다운으로 1회 구동합니다(준비 시 타겟 탐색·발사·재적재). </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">런타임 인스턴스</param>
        /// <param name="deltaTime">경과 시간(초)</param>
        public void DriveAutonomously(in AbilityContext ctx, AbilityInstance self, float deltaTime)
        {
            if (!TickCooldown(self, deltaTime))
            {
                return;
            }

            ITargetable target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, ctx.Range) : null;
            if (target == null)
            {
                return;
            }

            Fire(ctx, self, target);
            ResetCooldown(self, ctx);
        }
    }
}
