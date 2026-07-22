using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Combat;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Tests.EditMode
{
    public sealed class CoreWeaponBasicAttackTests
    {
        private sealed class BasicActive : AutoAbilityData
        {
            public int fireCount;
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { fireCount++; }
        }
        private sealed class OtherActive : AutoAbilityData
        {
            public int fireCount;
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target) { fireCount++; }
        }
        private sealed class StubTarget : ITargetable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsActive => true;
            public ActorState CurrentState => ActorState.Moving;
            public void SetState(ActorState newState) { }
            public event System.Action<ActorState> StateChanged;
        }

        private static BasicActive MakeBasic()
        {
            BasicActive a = ScriptableObject.CreateInstance<BasicActive>();
            a.tier = AbilityTier.Basic;
            return a;
        }
        private static AbilityContext Ctx()
        {
            return new AbilityContext(null, Vector3.zero, null, new AbilityModifiers(), null, new CombatStats());
        }

        private readonly List<GameObject> created = new List<GameObject>();

        /// <summary> 테스트에서 생성한 GameObject를 정리합니다. </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            created.Clear();
        }

        /// <summary> 사거리 내 활성 적 1체를 등록한 실제 타겟팅 컨텍스트를 만듭니다. </summary>
        /// <param name="attackSpeed">CombatStats에 설정할 공격속도</param>
        private AbilityContext MakeCtx(float attackSpeed = 1f)
        {
            EnemyRegistry registry = new EnemyRegistry();
            GameObject go = new GameObject("TestTarget");
            created.Add(go);

            MonsterActor actor = go.AddComponent<MonsterActor>();
            EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
            data.health = 10f;
            actor.Initialize(data);
            registry.Register(actor);

            TargetFinder finder = new TargetFinder(registry);
            CombatStats stats = new CombatStats();
            stats.attackSpeed = attackSpeed;
            return new AbilityContext(null, Vector3.zero, finder, new AbilityModifiers(), null, stats);
        }

        [Test]
        public void FireAll_FiresOnlyBasicAttack()
        {
            var loadout = new AbilityLoadout();
            BasicActive basic = MakeBasic();
            OtherActive other = ScriptableObject.CreateInstance<OtherActive>();
            other.tier = AbilityTier.Signature;
            loadout.TryAdd(basic);
            loadout.TryAdd(other);
            var weapon = new CoreWeapon(loadout, null);

            weapon.AimAt(new StubTarget());
            weapon.FireAll(Ctx());

            Assert.AreEqual(1, basic.fireCount, "기본 공격은 발사");
            Assert.AreEqual(0, other.fireCount, "그 외 능력은 CoreWeapon이 발사하지 않음");
        }

        [Test]
        public void BasicAttack_IsTheBasicTier()
        {
            var loadout = new AbilityLoadout();
            BasicActive basic = MakeBasic();
            loadout.TryAdd(basic);
            var weapon = new CoreWeapon(loadout, null);
            Assert.AreSame(basic, weapon.BasicAttack.data);
        }

        [Test]
        public void FireAll_WithoutAimedTarget_DoesNothing()
        {
            var loadout = new AbilityLoadout();
            BasicActive basic = MakeBasic();
            loadout.TryAdd(basic);
            var weapon = new CoreWeapon(loadout, null);

            weapon.FireAll(Ctx());

            Assert.AreEqual(0, basic.fireCount, "AimAt 없이는 발사하지 않음");
        }

        [Test]
        public void Tick_WithoutBasicAttack_DoesNothing()
        {
            var loadout = new AbilityLoadout();
            OtherActive other = ScriptableObject.CreateInstance<OtherActive>();
            other.tier = AbilityTier.Signature;
            loadout.TryAdd(other);
            var weapon = new CoreWeapon(loadout, null);

            weapon.Tick(Ctx(), 1f);

            Assert.AreEqual(0, other.fireCount, "Basic이 없으면 Tick이 아무 것도 하지 않음");
        }

        [Test]
        public void Tick_FiresBasicAttack_WhenTargetAvailable()
        {
            var loadout = new AbilityLoadout();
            BasicActive basic = MakeBasic();
            loadout.TryAdd(basic);
            var weapon = new CoreWeapon(loadout, null);

            weapon.Tick(MakeCtx(), 1f);

            Assert.AreEqual(1, basic.fireCount, "타겟이 있으면 기본 공격이 발사됨");
        }

        [Test]
        public void Tick_RespectsAttackSpeedInterval()
        {
            var loadout = new AbilityLoadout();
            BasicActive basic = MakeBasic();
            loadout.TryAdd(basic);
            var weapon = new CoreWeapon(loadout, null);
            AbilityContext ctx = MakeCtx(2f);   // 공격속도 2 → 간격 0.5초

            weapon.Tick(ctx, 0.1f);
            Assert.AreEqual(1, basic.fireCount, "첫 Tick은 남은시간 0에서 시작해 즉시 발사");

            weapon.Tick(ctx, 0.1f);
            Assert.AreEqual(1, basic.fireCount, "간격(0.5초)이 차기 전에는 재발사되지 않음");
        }
    }
}
