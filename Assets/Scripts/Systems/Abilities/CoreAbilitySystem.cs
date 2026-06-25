// 코어 능력 구동 — 로드아웃·러너 보유 + 시전 호스트(ICastHost): 발사 프레임에 대기 발사 실행
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> Arena 코어의 능력 로드아웃을 구동하고, 시전 애니 발사를 중계하는 컴포넌트입니다. </summary>
    public sealed class CoreAbilitySystem : MonoBehaviour, ICastHost
    {
        private AbilityLoadout loadout;
        private AbilityRunner runner;
        private GameFlowModel flow;
        private AbilityContext ctx;          // 공용 컨텍스트(모든 능력 공유)
        private ICastReceiver castReceiver;

        // 대기 발사 (시전 시작 ~ 발사 프레임)
        private ActiveAbilityData pendingSkill;
        private AbilityInstance pendingSelf;
        private ITargetable pendingTarget;

        /// <summary> 시전 비주얼을 연결합니다. </summary>
        public void SetCastReceiver(ICastReceiver receiver) => castReceiver = receiver;

        /// <summary> 합성 루트가 의존성·스타터 능력을 주입합니다. </summary>
        public void Setup(TargetFinder finder, Vector3 origin, GameFlowModel gameFlow,
            IReadOnlyList<AbilityData> starters)
        {
            flow = gameFlow;
            loadout = new AbilityLoadout();
            if (starters != null)
                for (int i = 0; i < starters.Count; i++)
                    if (starters[i] != null) loadout.TryAdd(starters[i]);

            IEffectSpawner effects = new SimpleEffectSpawner();
            ctx = new AbilityContext(this, origin, finder, loadout.Modifiers, effects, this);
            runner = new AbilityRunner(loadout, ctx);
            runner.EquipAll();
        }

        private void Update()
        {
            if (runner == null || flow == null || !flow.IsPlaying) return;
            runner.Tick(Time.deltaTime);
        }

        #region ICastHost
        /// <summary> 시전을 요청합니다. 비주얼이 시전 중이면 거부(false)합니다. </summary>
        public bool RequestCast(ActiveAbilityData skill, AbilityInstance self, ITargetable target, AnimationClip clip)
        {
            if (castReceiver == null || castReceiver.IsCasting) return false;
            pendingSkill = skill;
            pendingSelf = self;
            pendingTarget = target;
            castReceiver.PlayCast(clip, target);
            return true;
        }

        /// <summary> 애니메이션 발사 프레임 — 대기 중인 발사를 실행합니다. </summary>
        public void NotifyFireFrame()
        {
            if (pendingSkill == null) return;
            ActiveAbilityData skill = pendingSkill;
            pendingSkill = null;
            skill.FireFromHost(ctx, pendingSelf, pendingTarget);
        }
        #endregion
    }
}
