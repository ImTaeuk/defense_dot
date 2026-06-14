using UnityEngine;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력 1회 발동에 필요한 입력 묶음(Context Object)입니다. </summary>
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

        public AbilityContext(MonoBehaviour host, Vector3 origin, TargetFinder finder, AbilityModifiers modifiers)
        {
            Host = host;
            Origin = origin;
            Finder = finder;
            Modifiers = modifiers;
        }
    }
}
