using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Tests.EditMode
{
    public class EnemyBehaviorTreeTests
    {
        /// <summary> 도달 여부를 제어하는 테스트용 이동 전략. </summary>
        private sealed class FakeMovement : IMovementStrategy
        {
            public bool Reached;
            public int TickCount { get; private set; }
            public void Tick(float deltaTime) { TickCount++; }
            public bool HasReachedGoal => Reached;
        }

        private static MonsterActor MakeMonster(FakeMovement mv)
        {
            var go = new GameObject("enemy");
            var actor = go.AddComponent<MonsterActor>();
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.health = 10f;
            actor.Initialize(data);
            actor.OnSpawn();                 // 상태 Idle
            actor.SetMovement(mv);
            return actor;
        }

        [Test]
        public void WithMovement_SetsMoving()
        {
            var mv = new FakeMovement { Reached = false };
            var actor = MakeMonster(mv);
            var brain = actor.gameObject.AddComponent<EnemyBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Moving, actor.CurrentState);
            Assert.AreEqual(1, mv.TickCount, "리프가 이동 전략을 tick 해야 함");
            Object.DestroyImmediate(actor.gameObject);
        }

        [Test]
        public void ReachedGoal_TransitionsToDead()
        {
            var mv = new FakeMovement { Reached = true };
            var actor = MakeMonster(mv);
            var brain = actor.gameObject.AddComponent<EnemyBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Dead, actor.CurrentState, "도달 시 HandleReachedGoal→Dead");
            Object.DestroyImmediate(actor.gameObject);
        }
    }
}
