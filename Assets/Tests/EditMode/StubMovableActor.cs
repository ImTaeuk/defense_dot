using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> 테스트용 IMovableActor 스텁입니다. </summary>
    public sealed class StubMovableActor : IMovableActor
    {
        public Vector3 LastPosition { get; private set; }
        public bool Movable = true;

        public Vector3 Position => LastPosition;
        public ActorState CurrentState => ActorState.Moving;
        public void SetState(ActorState newState) { }
        public void SetPosition(Vector3 newPosition) => LastPosition = newPosition;
        public bool IsMovableState() => Movable;
    }
}
