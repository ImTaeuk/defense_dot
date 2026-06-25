// 시전 요청 계약 — 능력이 애니 동반 발사를 요청·발사 프레임을 수신
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 능력이 시전(애니메이션을 동반한 발사)을 요청하는 대상 계약입니다.
    /// 시전 비주얼(ICastReceiver)을 구동하고, 발사 프레임에 대기 발사를 실행합니다.
    /// </summary>
    public interface ICastHost
    {
        /// <summary> 시전을 요청합니다. 수락되면(시전 시작) true, 이미 시전 중이면 false. </summary>
        bool RequestCast(ActiveAbilityData skill, AbilityInstance self, ITargetable target, UnityEngine.AnimationClip clip);

        /// <summary> 애니메이션 발사 프레임 도달을 통지합니다. (대기 중인 발사 실행) </summary>
        void NotifyFireFrame();
    }
}
