using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Tests.EditMode
{
    /// <summary> ActorBase.StateChanged 이벤트 발화를 검증합니다. </summary>
    public sealed class ActorStateEventTests
    {
        [Test]
        public void SetState_DifferentState_RaisesStateChanged()
        {
            var go = new GameObject("Enemy");
            var actor = go.AddComponent<MonsterActor>();
            ActorState received = ActorState.Idle;
            int count = 0;
            ((IActor)actor).StateChanged += s => { received = s; count++; };

            ((IActor)actor).SetState(ActorState.Moving);

            Assert.AreEqual(1, count);
            Assert.AreEqual(ActorState.Moving, received);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetState_SameState_DoesNotRaise()
        {
            var go = new GameObject("Enemy");
            var actor = go.AddComponent<MonsterActor>();
            int count = 0;
            ((IActor)actor).StateChanged += _ => count++;

            ((IActor)actor).SetState(ActorState.Idle);

            Assert.AreEqual(0, count);
            Object.DestroyImmediate(go);
        }
    }
}
