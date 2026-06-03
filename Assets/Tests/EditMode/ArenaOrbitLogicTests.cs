using NUnit.Framework;
using UnityEngine;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Tests.EditMode
{
    public class ArenaOrbitLogicTests
    {
        private ArenaModel MakeArena()
        {
            var m = new ArenaModel();
            m.Initialize(29f, 2.2f, 4f, 2f, 80); // min 6.2, max 27
            return m;
        }

        [Test]
        public void Tick_PlacesEnemyAtRatioRadius()
        {
            var actor = new StubMovableActor();
            var arena = MakeArena();
            // startAngle 0, startRatio 0.5, angularSpeed 0 → radius = Lerp(6.2, 27, 0.5) = 16.6
            var logic = new ArenaOrbitLogic(actor, Vector3.zero, arena, 0f, 0.5f, 0f, 0.8f);
            logic.Tick(0f);
            Assert.AreEqual(16.6f, actor.LastPosition.x, 0.01f);
            Assert.AreEqual(0.8f, actor.LastPosition.y, 0.01f);
            Assert.AreEqual(0f, actor.LastPosition.z, 0.01f);
        }

        [Test]
        public void Tick_CompressesWhenArenaShrinks()
        {
            var actor = new StubMovableActor();
            var arena = MakeArena();
            var logic = new ArenaOrbitLogic(actor, Vector3.zero, arena, 0f, 0.5f, 0f, 0.8f);
            logic.Tick(0f);
            float before = actor.LastPosition.x; // 16.6
            arena.Shrink(9f); // max 27→18, min 6.2 → Lerp(6.2,18,0.5)=12.1
            logic.Tick(0f);
            float after = actor.LastPosition.x;
            Assert.Less(after, before);
            Assert.AreEqual(12.1f, after, 0.01f);
        }

        [Test]
        public void Tick_DoesNotMoveWhenNotMovable()
        {
            var actor = new StubMovableActor { Movable = false };
            var arena = MakeArena();
            var logic = new ArenaOrbitLogic(actor, Vector3.zero, arena, 0f, 0.5f, 0f, 0.8f);
            logic.Tick(1f);
            Assert.AreEqual(Vector3.zero, actor.LastPosition); // SetPosition 미호출
        }

        [Test]
        public void HasReachedGoal_AlwaysFalse()
        {
            var logic = new ArenaOrbitLogic(new StubMovableActor(), Vector3.zero, MakeArena(), 0f, 0.5f, 0f, 0.8f);
            Assert.IsFalse(logic.HasReachedGoal);
        }
    }
}
