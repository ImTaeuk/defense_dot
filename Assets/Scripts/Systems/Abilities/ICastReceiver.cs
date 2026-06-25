// 시전 비주얼 계약 — 지정 클립으로 시전 애니 재생, 시전 중 여부 노출
namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 시전 애니메이션을 재생하는 비주얼 계약입니다. (Tower의 Animator 오버라이드 구동)
    /// </summary>
    public interface ICastReceiver
    {
        /// <summary> 지정 클립으로 시전 애니메이션을 재생하고, 시전 동안 대상을 향해 조준합니다. </summary>
        void PlayCast(UnityEngine.AnimationClip clip, DefenseDot.Core.ITargetable target);

        /// <summary> 현재 시전(애니 재생) 중인지 여부입니다. </summary>
        bool IsCasting { get; }
    }
}
