using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Tests.EditMode
{
    public class TowerBehaviorTreeTests
    {
        private sealed class FakeTarget : ITargetable
        {
            public Vector3 Pos;
            public Vector3 Position => Pos;
            public bool IsActive => true;
            public ActorState CurrentState => ActorState.Moving;
            public void SetState(ActorState newState) { }
            public event System.Action<ActorState> StateChanged;
        }

        private static TowerActor MakeTower()
        {
            var go = new GameObject("tower");
            go.transform.position = Vector3.zero;
            var actor = go.AddComponent<TowerActor>();
            var data = ScriptableObject.CreateInstance<TowerData>();
            data.attackRange = 5f;
            data.attackSpeed = 1f;
            actor.Initialize(data);
            actor.OnSpawn();
            return actor;
        }

        [Test]
        public void NoTarget_SetsIdle()
        {
            var actor = MakeTower();
            var brain = actor.gameObject.AddComponent<TowerBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Idle, actor.CurrentState);
            Object.DestroyImmediate(actor.gameObject);
        }

        [Test]
        public void TargetInRange_SetsAttacking()
        {
            var actor = MakeTower();
            actor.SetTarget(new FakeTarget { Pos = new Vector3(1f, 0f, 0f) });   // 사거리 내
            var brain = actor.gameObject.AddComponent<TowerBehaviorTree>();
            brain.Tick();
            Assert.AreEqual(ActorState.Attacking, actor.CurrentState);
            Object.DestroyImmediate(actor.gameObject);
        }
    }
}
