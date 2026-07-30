using UnityEngine;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities.Effects;
using DefenseDot.Systems.Combat;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력 구동 입력 묶음(Context Object)입니다. </summary>
    public readonly struct AbilityContext
    {
        /// <summary> 발동 원점(코어 중심). 타게팅·오비탈 궤도 중심 기준. </summary>
        public readonly Vector3 Origin;
        /// <summary> 발사체·머즐 스폰용 발사점(타워 총구). null이면 Origin으로 폴백. </summary>
        public readonly Transform FireOrigin;
        /// <summary> 적 질의 수단. </summary>
        public readonly TargetFinder Finder;
        /// <summary> 패시브 합산 보정. </summary>
        public readonly AbilityModifiers Modifiers;
        /// <summary> 효과 엔티티 스포너. </summary>
        public readonly IEffectSpawner Effects;
        /// <summary> 전투 능력치(공격속도·쿨다운 배율). </summary>
        public readonly CombatStats Stats;

        /// <summary> 발사 시점의 총구 월드 위치. 발사점 미배선이면 코어 중심(Origin). </summary>
        public Vector3 FirePosition => FireOrigin != null ? FireOrigin.position : Origin;

        /// <summary> 능력 구동에 필요한 입력을 묶습니다. </summary>
        /// <param name="origin">발동 원점(코어 중심)</param>
        /// <param name="finder">적 질의 수단</param>
        /// <param name="modifiers">패시브 합산 보정</param>
        /// <param name="effects">효과 엔티티 스포너</param>
        /// <param name="stats">전투 능력치</param>
        /// <param name="fireOrigin">발사체·머즐 스폰용 총구(없으면 origin 폴백)</param>
        public AbilityContext(Vector3 origin, TargetFinder finder,
            AbilityModifiers modifiers, IEffectSpawner effects, CombatStats stats, Transform fireOrigin = null)
        {
            Origin = origin;
            FireOrigin = fireOrigin;
            Finder = finder;
            Modifiers = modifiers;
            Effects = effects;
            Stats = stats;
        }
    }
}
