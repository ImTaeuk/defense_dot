// 코어 능력 구동 — 로드아웃·러너 보유 + 시전 호스트(ICastHost): 발사 프레임에 대기 발사 실행
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;
using DefenseDot.Core.Pooling;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> Arena 코어의 능력 로드아웃을 구동하고, 시전 애니 발사를 중계하는 컴포넌트입니다. </summary>
    public sealed class CoreAbilitySystem : MonoBehaviour, ICastHost, IAbilityCommandTarget
    {
        private AbilityLoadout loadout;      // 장착 능력 슬롯(액티브/패시브)
        private AbilityRunner runner;        // 능력 프레임 구동·장착 훅
        private GameFlowModel flow;          // 진행 단계(발동 게이트)
        private AbilityContext ctx;          // 공용 컨텍스트(모든 능력 공유)
        private ICastReceiver castReceiver;  // 시전 비주얼 수신자
        private PoolManager pool;            // 스타터 예열용

        // 대기 발사 (시전 시작 ~ 발사 프레임)
        private ActiveAbilityData pendingSkill;
        private AbilityInstance pendingSelf;
        private ITargetable pendingTarget;

        /// <summary> 시전 비주얼을 연결합니다. </summary>
        public void SetCastReceiver(ICastReceiver receiver) => castReceiver = receiver;

        /// <summary> 합성 루트가 의존성·스타터 능력을 주입합니다. fireOrigin은 발사체·머즐 스폰용 총구(없으면 origin 폴백). </summary>
        public void Setup(TargetFinder finder, Vector3 origin, GameFlowModel gameFlow,
            ICombatState combatState, IReadOnlyList<AbilityData> starters, PoolManager poolManager,
            Transform fireOrigin = null)
        {
            flow = gameFlow;
            pool = poolManager;
            loadout = new AbilityLoadout();
            loadout.Modifiers.combatState = combatState;
            if (starters != null)
                for (int i = 0; i < starters.Count; i++)
                    if (starters[i] != null) loadout.TryAdd(starters[i]);

            IEffectSpawner effects = new PooledEffectSpawner(poolManager);
            ctx = new AbilityContext(this, origin, finder, loadout.Modifiers, effects, this, fireOrigin);
            runner = new AbilityRunner(loadout, ctx);
            // 장착은 예열 후로 미룸(예열 전 Spawn 방지) → WarmupAndEquipAsync
        }

        /// <summary> 스타터 이펙트를 예열합니다(장착 전. 로드 실패는 값으로 스킵되어 예외 없음). </summary>
        public async UniTask WarmupStartersAsync()
        {
            if (pool == null || loadout == null) return;
            using (UnityEngine.Pool.HashSetPool<AssetReferenceGameObject>.Get(out HashSet<AssetReferenceGameObject> set))
            {
                CollectAssets(loadout.Actives, set);
                CollectAssets(loadout.Passives, set);
                if (set.Count > 0) await pool.WarmupAsync(set);
            }
        }

        /// <summary> 장착된 액티브 능력을 러너에 장착합니다. </summary>
        public void EquipAll() => runner?.EquipAll();

        /// <summary> 능력 목록의 예열 대상 에셋을 집합에 모읍니다. </summary>
        private static void CollectAssets(IReadOnlyList<AbilityInstance> list, HashSet<AssetReferenceGameObject> set)
        {
            for (int i = 0; i < list.Count; i++)
            {
                AbilityData d = list[i].data;
                if (d == null) continue;
                foreach (AssetReferenceGameObject a in d.EffectAssets)
                    if (a != null) set.Add(a);
            }
        }

        #region IAbilityCommandTarget
        /// <summary> 읽기 전용 로드아웃(카드 생성기 질의용). </summary>
        public AbilityLoadout Loadout => loadout;

        /// <summary> 신규 능력 추가. 액티브면 러너에 즉시 장착(라이프사이클 동기화). 추가된 인스턴스 반환(실패 시 null). </summary>
        public AbilityInstance AddAbility(AbilityData data)
        {
            if (loadout == null || !loadout.TryAdd(data)) return null;
            bool isActive = data is ActiveAbilityData;
            AbilityInstance inst = isActive
                ? loadout.Actives[loadout.Actives.Count - 1]
                : loadout.Passives[loadout.Passives.Count - 1];
            if (isActive) runner?.Equip(inst);
            return inst;
        }

        /// <summary> 기존 능력 레벨업. </summary>
        public void LevelUpAbility(AbilityInstance instance) => loadout?.LevelUp(instance);

        /// <summary> 능력 삭제. 액티브면 러너에서 언장착 후 로드아웃에서 제거합니다. </summary>
        public void RemoveAbility(AbilityInstance instance)
        {
            if (instance?.data is ActiveAbilityData) runner?.Unequip(instance);
            loadout?.Remove(instance);
        }
        #endregion

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
