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
        /// <summary> 발동 원점(코어 위치). </summary>
        public readonly Vector3 Origin;
        /// <summary> 적 질의 수단. </summary>
        public readonly TargetFinder Finder;
        /// <summary> 패시브 합산 보정. </summary>
        public readonly AbilityModifiers Modifiers;
        /// <summary> 효과 엔티티 스포너. </summary>
        public readonly IEffectSpawner Effects;
        /// <summary> 시전(애니 동반 발사) 요청 대상. null이면 즉시 발사. </summary>
        public readonly ICastHost Cast;

        public AbilityContext(MonoBehaviour host, Vector3 origin, TargetFinder finder,
            AbilityModifiers modifiers, IEffectSpawner effects, ICastHost cast = null)
        {
            Host = host;
            Origin = origin;
            Finder = finder;
            Modifiers = modifiers;
            Effects = effects;
            Cast = cast;
        }
    }
}
