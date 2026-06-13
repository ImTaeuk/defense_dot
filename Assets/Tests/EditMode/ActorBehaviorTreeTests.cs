using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class ActorBehaviorTreeTests
    {
        /// <summary> IActor를 구현하고 상태를 기록하는 테스트용 MonoBehaviour. </summary>
        private sealed class StubActorComponent : MonoBehaviour, IActor
        {
            public ActorState LastSet { get; private set; } = ActorState.Idle;
            public Vector3 Position => transform.position;
            public ActorState CurrentState => LastSet;
            public void SetState(ActorState newState) { LastSet = newState; StateChanged?.Invoke(newState); }
            public event System.Action<ActorState> StateChanged;
        }

        /// <summary> primary가 Moving을 쓰는 최소 파생 트리. </summary>
        private sealed class TestBehaviorTree : ActorBehaviorTree
        {
            protected override BTNode BuildPrimary()
            {
                return BT.Action(bb => { actor.SetState(ActorState.Moving); return NodeStatus.Running; });
            }
        }

        [Test]
        public void StunActive_SetsStunned_SkipsPrimary()
        {
            var go = new GameObject("a");
            go.AddComponent<StubActorComponent>();
            var brain = go.AddComponent<TestBehaviorTree>();
            brain.Blackboard.stunTimer = 1f;     // stun 의도 주입
            brain.Tick();
            Assert.AreEqual(ActorState.Stunned, brain.Actor.CurrentState, "stun 활성 시 Stunned, primary(Moving) 차단");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void NoStun_RunsPrimary()
        {
            var go = new GameObject("a");
            go.AddComponent<StubActorComponent>();
            var brain = go.AddComponent<TestBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Moving, brain.Actor.CurrentState, "stun 없으면 primary 실행");
            Object.DestroyImmediate(go);
        }
    }
}
