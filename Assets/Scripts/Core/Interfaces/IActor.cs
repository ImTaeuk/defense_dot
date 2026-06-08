using UnityEngine;

namespace DefenseDot.Core
{
    /// <summary>
    /// 모든 액터의 기본 인터페이스입니다.
    /// </summary>
    public interface IActor
    {
        /// <summary> 액터의 현재 월드 위치 </summary>
        Vector3 Position { get; }
        
        /// <summary> 액터의 현재 상태 </summary>
        ActorState CurrentState { get; }

        /// <summary> 액터의 상태를 변경 </summary>
        void SetState(ActorState newState);

        /// <summary> 상태 변경 시 발생 (변경된 새 상태 전달) </summary>
        event System.Action<ActorState> StateChanged;
    }
}
