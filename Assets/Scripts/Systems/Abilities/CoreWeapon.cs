using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 코어의 무기입니다. 주축 1개와 동반 N개를 로드아웃에서 읽어, 발사 주기를 계산하고
    /// 모션 속도를 지시하고 발사 프레임에 전부 발사합니다. 공격 주기의 유일한 소유자입니다.
    /// </summary>
    public sealed class CoreWeapon
    {
        /// <summary> 발사 주기 하한(초). </summary>
        private const float MinCycle = 0.05f;

        /// <summary> 장착 능력의 원천. 여기서 주축·동반을 읽습니다. </summary>
        private readonly AbilityLoadout loadout;
        /// <summary> 공격 모션 재생 대상. null이면 모션 없이 즉시 발사합니다. </summary>
        private readonly IAttackMotion motion;
        /// <summary> 현재 장착된 동반 공격 능력들. </summary>
        private readonly List<AbilityInstance> subs = new List<AbilityInstance>();

        /// <summary> 현재 장착된 주축 공격 능력. 없으면 null. </summary>
        private AbilityInstance main;
        /// <summary> 타워 기본 공격 주기(초). </summary>
        private float baseCycle = 1f;
        /// <summary> 다음 발사까지 남은 시간(초). </summary>
        private float remaining;
        /// <summary> 이번 발사 묶음의 대상. </summary>
        private ITargetable pendingTarget;

        /// <summary> 현재 장착된 주축 공격 능력. 없으면 null. </summary>
        public AbilityInstance Main => main;

        /// <summary> 직전에 계산된 발사 주기(초). </summary>
        public float Cycle { get; private set; } = 1f;

        /// <summary> 로드아웃을 구독해 주축·동반을 추적합니다. </summary>
        /// <param name="loadout">장착 능력의 원천</param>
        /// <param name="motion">공격 모션 재생 대상(없으면 null)</param>
        public CoreWeapon(AbilityLoadout loadout, IAttackMotion motion)
        {
            this.loadout = loadout;
            this.motion = motion;
            if (loadout != null)
            {
                loadout.OnChanged += Rebuild;
                Rebuild();
            }
        }

        /// <summary> 타워 기본 공격 속도를 설정합니다. </summary>
        /// <param name="attacksPerSecond">초당 공격 횟수</param>
        public void SetBaseAttackSpeed(float attacksPerSecond)
        {
            baseCycle = 1f / Mathf.Max(0.01f, attacksPerSecond);
        }

        /// <summary> 로드아웃 구독을 해제합니다. </summary>
        public void Detach()
        {
            if (loadout != null)
                loadout.OnChanged -= Rebuild;
        }

        /// <summary> 현재 구성의 발사 주기(초)를 계산합니다. </summary>
        /// <param name="ctx">쿨다운 감소 보정을 읽을 컨텍스트</param>
        public float CalculateCycle(in AbilityContext ctx)
        {
            float cycle = baseCycle;
            if (main != null && main.data is MainAbilityData mainData)
                cycle += mainData.cycleDelta;

            for (int i = 0; i < subs.Count; i++)
            {
                if (subs[i].data is SubAbilityData subData)
                    cycle += subData.cycleDelta;
            }

            if (ctx.Modifiers != null)
                cycle -= ctx.Modifiers.cooldownReduction;

            return Mathf.Max(MinCycle, cycle);
        }

        /// <summary> 이번 발사 묶음의 대상을 지정합니다(모션 없는 경로·테스트용). </summary>
        /// <param name="target">발사 대상</param>
        public void AimAt(ITargetable target)
        {
            pendingTarget = target;
        }

        /// <summary> 새 능력을 받기 위해 해제해야 할 기존 주축을 반환합니다(주축은 1개만 보유). </summary>
        /// <param name="incoming">새로 장착하려는 능력 설계도</param>
        public AbilityInstance FindMainToReplace(AbilityData incoming)
        {
            if (incoming is MainAbilityData)
                return main;

            return null;
        }

        /// <summary> 주기를 진행시키고, 준비되면 모션을 시작하거나 즉시 발사합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="deltaTime">경과 시간(초)</param>
        public void Tick(in AbilityContext ctx, float deltaTime)
        {
            if (main == null)
                return;

            remaining -= deltaTime;
            if (remaining > 0f)
                return;

            MainAbilityData mainData = main.data as MainAbilityData;
            if (mainData == null)
                return;

            // 타겟이 없으면 남은시간을 되돌리지 않아, 잡히는 즉시 발사된다
            ITargetable target = ctx.Finder != null
                ? ctx.Finder.FindNearest(ctx.Origin, mainData.TargetRange)
                : null;
            if (target == null)
                return;

            pendingTarget = target;
            Cycle = CalculateCycle(ctx);
            remaining = Cycle;

            // 모션이 있으면 발사 프레임이 FireAll 을 호출하고, 없으면 지금 발사한다
            AnimationClip clip = mainData.CastAnimation;
            if (motion != null && clip != null)
            {
                float speed = clip.length / Mathf.Max(MinCycle, Cycle);
                motion.PlayAttack(clip, target, speed);
            }
            else
            {
                FireAll(ctx);
            }
        }

        /// <summary> 주축과 모든 동반 능력을 한 번에 발사합니다(모션의 발사 프레임이 호출). </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        public void FireAll(in AbilityContext ctx)
        {
            if (main == null || pendingTarget == null)
                return;

            if (main.data is ActiveAbilityData mainActive)
                mainActive.FireFromWeapon(ctx, main, pendingTarget);

            for (int i = 0; i < subs.Count; i++)
            {
                if (subs[i].data is ActiveAbilityData subActive)
                    subActive.FireFromWeapon(ctx, subs[i], pendingTarget);
            }
        }

        /// <summary> 로드아웃에서 주축·동반을 다시 읽습니다(로드아웃 변경 시 호출). </summary>
        private void Rebuild()
        {
            main = null;
            subs.Clear();
            if (loadout == null)
                return;

            IReadOnlyList<AbilityInstance> actives = loadout.Actives;
            for (int i = 0; i < actives.Count; i++)
            {
                AbilityInstance inst = actives[i];
                if (inst.data is MainAbilityData)
                    main = inst;
                else if (inst.data is SubAbilityData)
                    subs.Add(inst);
            }
        }
    }
}
