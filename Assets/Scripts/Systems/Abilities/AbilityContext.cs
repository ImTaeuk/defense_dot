using UnityEngine;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력 구동 입력 묶음(Context Object)입니다. </summary>
    public readonly struct AbilityContext
    {
        /// <summary> 투사체 생성 등에 쓰는 호스트 MonoBehaviour. </summary>
        public readonly MonoBehaviour Host;
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

        /// <summary> 발사 시점의 총구 월드 위치. 발사점 미배선이면 코어 중심(Origin). </summary>
        public Vector3 FirePosition => FireOrigin != null ? FireOrigin.position : Origin;

        public AbilityContext(MonoBehaviour host, Vector3 origin, TargetFinder finder,
            AbilityModifiers modifiers, IEffectSpawner effects, Transform fireOrigin = null)
        {
            Host = host;
            Origin = origin;
            FireOrigin = fireOrigin;
            Finder = finder;
            Modifiers = modifiers;
            Effects = effects;
        }
    }
}
