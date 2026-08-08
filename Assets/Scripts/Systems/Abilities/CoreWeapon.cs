using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 코어의 기본 공격 구동자입니다. 기본 공격(tier==Basic)의 애니메이션을 공격속도에 맞춰
    /// 재생하고, 발사 프레임에 기본 공격만 발사합니다. 그 외 능력은 AbilityRunner가 각자 쿨다운으로 구동합니다.
    /// </summary>
    public sealed class CoreWeapon
    {
        /// <summary> 공격 간격 하한(초). </summary>
        private const float MIN_INTERVAL = 0.05f;

        /// <summary> 장착 능력의 원천. 여기서 기본 공격을 읽습니다. </summary>
        private readonly AbilityLoadout loadout;
        /// <summary> 공격 모션 재생 대상. null이면 모션 없이 즉시 발사합니다. </summary>
        private readonly IAttackMotion motion;

        /// <summary> 기본 공격 모션 클립(캐릭터 소유, 생성자 주입 고정). </summary>
        private readonly AnimationClip castClip;

        /// <summary> 현재 기본 공격. 없으면 null. </summary>
        private AbilityInstance basicAttack;
        /// <summary> 다음 발사까지 남은 시간(초). </summary>
        private float remaining;
        /// <summary> 이번 발사의 대상. </summary>
        private ITargetable pendingTarget;

        /// <summary> 현재 기본 공격. 없으면 null. </summary>
        public AbilityInstance BasicAttack => basicAttack;

        /// <summary> 로드아웃을 구독해 기본 공격을 추적합니다. </summary>
        /// <param name="loadout">장착 능력의 원천</param>
        /// <param name="motion">공격 모션 재생 대상(없으면 null)</param>
        /// <param name="castClip">기본 공격 모션 클립(캐릭터 소유, 없으면 즉시 발사)</param>
        public CoreWeapon(AbilityLoadout loadout, IAttackMotion motion, AnimationClip castClip = null)
        {
            this.loadout = loadout;
            this.motion = motion;
            this.castClip = castClip;
            if (loadout != null)
            {
                loadout.OnChanged += Rebuild;
                Rebuild();
            }
        }

        /// <summary> 로드아웃 구독을 해제합니다. </summary>
        public void Detach()
        {
            if (loadout != null)
            {
                loadout.OnChanged -= Rebuild;
            }
        }

        /// <summary> 이번 발사의 대상을 지정합니다(모션 없는 경로·테스트용). </summary>
        /// <param name="target">발사 대상</param>
        public void AimAt(ITargetable target)
        {
            pendingTarget = target;
        }

        /// <summary> 공격속도로 애니 속도를 정해 재생하거나, 준비되면 즉시 발사합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="deltaTime">경과 시간(초)</param>
        public void Tick(in AbilityContext ctx, float deltaTime)
        {
            if (basicAttack == null)
            {
                return;
            }

            remaining -= deltaTime;
            if (remaining > 0f)
            {
                return;
            }

            ITargetable target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, ctx.Range) : null;
            if (target == null)
            {
                return;   // 준비 유지 — 타겟 잡히는 즉시 발사
            }

            pendingTarget = target;
            float attackSpeed = ctx.Stats != null ? ctx.Stats.attackSpeed : 1f;
            float interval = 1f / Mathf.Max(0.01f, attackSpeed);
            remaining = interval;

            if (motion != null && castClip != null)
            {
                float playSpeed = castClip.length / Mathf.Max(MIN_INTERVAL, interval);
                motion.PlayAttack(castClip, target, playSpeed);
            }
            else
            {
                FireAll(ctx);
            }
        }

        /// <summary> 기본 공격만 발사합니다(모션의 발사 프레임이 호출). </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        public void FireAll(in AbilityContext ctx)
        {
            if (basicAttack == null || pendingTarget == null)
            {
                return;
            }

            if (basicAttack.data is ActiveAbilityData active)
            {
                active.FireFromWeapon(ctx, basicAttack, pendingTarget);
            }
        }

        /// <summary> 로드아웃에서 기본 공격(tier==Basic)을 다시 읽습니다(로드아웃 변경 시 호출). </summary>
        private void Rebuild()
        {
            basicAttack = null;
            if (loadout == null)
            {
                return;
            }

            IReadOnlyList<AbilityInstance> actives = loadout.Actives;
            for (int i = 0; i < actives.Count; i++)
            {
                AbilityInstance inst = actives[i];
                if (inst.data.tier != AbilityTier.Basic)
                {
                    continue;
                }

                basicAttack = inst;
                return;
            }
        }
    }
}
