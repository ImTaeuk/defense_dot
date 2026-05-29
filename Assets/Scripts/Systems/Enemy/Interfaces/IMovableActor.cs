using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 이동 가능한 액터가 구현해야 하는 인터페이스입니다.
    /// </summary>
    public interface IMovableActor : IActor
    {
        /// <summary> 액터의 위치를 직접 설정 </summary>
        void SetPosition(Vector3 newPosition);

        /// <summary> 현재 액터가 이동 가능한 상태(State)인지 반환 </summary>
        bool IsMovableState();
    }
}
