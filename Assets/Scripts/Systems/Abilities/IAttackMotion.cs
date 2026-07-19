using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 공격 모션 재생 대상(타워 비주얼)입니다. </summary>
    public interface IAttackMotion
    {
        /// <summary> 공격 모션을 지정 속도로 재생하고 대상을 향해 조준합니다. </summary>
        /// <param name="clip">재생할 공격 모션</param>
        /// <param name="target">조준 대상</param>
        /// <param name="speed">재생 속도 배수(클립 길이 ÷ 발사 주기)</param>
        void PlayAttack(AnimationClip clip, ITargetable target, float speed);
    }
}
