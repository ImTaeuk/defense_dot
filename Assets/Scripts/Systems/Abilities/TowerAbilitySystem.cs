// 타워 능력 구동 — 로드아웃·러너·무기 보유. 기본 공격은 무기가, 그 외 자율 능력은 러너가 구동
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;
using DefenseDot.Core.Pooling;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities.Effects;
using DefenseDot.Systems.Combat;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> Arena 타워의 능력 로드아웃과 무기를 구동합니다. 소유자가 Tick으로 진행시킵니다. </summary>
    public sealed class TowerAbilitySystem : IAbilityCommandTarget
    {
        private AbilityLoadout loadout;      // 장착 능력 슬롯(액티브/패시브)
        private AbilityRunner runner;        // 자율 능력 프레임 구동·장착 훅
        private CoreWeapon weapon;           // 기본 공격 구동(공격속도로 애니 재생)
        private AbilityContext ctx;          // 공용 컨텍스트(모든 능력 공유)
        private IAttackMotion motion;        // 공격 모션 재생 대상
        private PoolSystem pool;            // 스타터 예열용
        private float baseAttackSpeed = 1f;  // 타워 기본 공격 속도(초당 횟수)
        private AnimationClip castAnimation;   // 캐릭터 기본 공격 모션(무기 생성 전에 주입)
        private readonly CombatStats stats = new CombatStats();   // 전투 능력치(ctx로 주입)

        /// <summary> 공격 모션 재생 대상을 연결합니다(무기 생성 전에 호출). </summary>
        /// <param name="attackMotion">타워 비주얼</param>
        public void SetAttackMotion(IAttackMotion attackMotion)
        {
            motion = attackMotion;
            weapon?.Detach();
            weapon = null;
        }

        /// <summary> 캐릭터 기본 공격 모션을 연결합니다(무기 생성 전에 호출). </summary>
        /// <param name="clip">기본 공격 클립(없으면 즉시 발사)</param>
        public void SetCastAnimation(AnimationClip clip)
        {
            castAnimation = clip;
            weapon?.Detach();
            weapon = null;
        }

        /// <summary> 타워 기본 공격 속도를 설정합니다. </summary>
        /// <param name="attacksPerSecond">초당 공격 횟수</param>
        public void SetBaseAttackSpeed(float attacksPerSecond)
        {
            baseAttackSpeed = attacksPerSecond;
            stats.attackSpeed = Mathf.Max(0.01f, attacksPerSecond);
        }

        /// <summary> 합성 루트가 의존성·스타터 능력을 주입합니다. fireOrigin은 발사체·머즐 스폰용 총구(없으면 origin 폴백). </summary>
        public void Setup(TargetFinder finder, Vector3 origin,
            ICombatState combatState, IReadOnlyList<AbilityData> starters, PoolSystem poolSystem,
            Transform fireOrigin = null, float range = 30f)
        {
            pool = poolSystem;
            loadout = new AbilityLoadout();
            loadout.Modifiers.combatState = combatState;
            if (starters != null)
            {
                for (int i = 0; i < starters.Count; i++)
                {
                    if (starters[i] != null) loadout.TryAdd(starters[i]);
                }
            }

            IEffectSpawner effects = new PooledEffectSpawner(poolSystem);
            ctx = new AbilityContext(origin, finder, loadout.Modifiers, effects, stats, fireOrigin, range);
            runner = new AbilityRunner(loadout, ctx);
            weapon = new CoreWeapon(loadout, motion, castAnimation);
            // 장착은 예열 후로 미룸(예열 전 Spawn 방지) → WarmupStartersAsync → EquipAll
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
                {
                    if (a != null) set.Add(a);
                }
            }
        }

        #region IAbilityCommandTarget
        /// <summary> 읽기 전용 로드아웃(카드 생성기 질의용). </summary>
        public AbilityLoadout Loadout => loadout;

        /// <summary> 신규 능력 추가. </summary>
        /// <param name="data">추가할 능력 설계도</param>
        public AbilityInstance AddAbility(AbilityData data)
        {
            if (loadout == null) return null;

            if (!loadout.TryAdd(data)) return null;
            bool isActive = data is ActiveAbilityData;
            AbilityInstance inst = isActive
                ? loadout.Actives[loadout.Actives.Count - 1]
                : loadout.Passives[loadout.Passives.Count - 1];
            if (isActive) runner?.Equip(inst);
            return inst;
        }

        /// <summary> 기존 능력 레벨업. </summary>
        /// <param name="instance">레벨업할 인스턴스</param>
        public void LevelUpAbility(AbilityInstance instance) => loadout?.LevelUp(instance);

        /// <summary> 능력 삭제. 액티브면 러너에서 언장착 후 로드아웃에서 제거합니다. </summary>
        /// <param name="instance">제거할 인스턴스</param>
        public void RemoveAbility(AbilityInstance instance)
        {
            if (instance?.data is ActiveAbilityData) runner?.Unequip(instance);
            loadout?.Remove(instance);
        }
        #endregion

        /// <summary> 공격 모션의 발사 프레임에서 비주얼이 호출합니다. </summary>
        public void NotifyFireFrame()
        {
            weapon?.FireAll(ctx);
        }

        /// <summary> 능력과 무기를 한 프레임 진행시킵니다. </summary>
        /// <param name="deltaTime">경과 시간</param>
        public void Tick(float deltaTime)
        {
            runner?.Tick(deltaTime);
            weapon?.Tick(ctx, deltaTime);
        }

        /// <summary> 무기를 떼어 정리합니다. </summary>
        public void Dispose()
        {
            weapon?.Detach();
        }
    }
}
