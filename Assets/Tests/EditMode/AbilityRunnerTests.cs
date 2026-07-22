using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Combat;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Tests.EditMode
{
    public class AbilityRunnerTests
    {
        private sealed class LifeAbility : AutoAbilityData, IAbilityLifecycle
        {
            public int equips, unequips;
            public override void Tick(in AbilityContext ctx, AbilityInstance self, float dt) { }
            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
            public void OnEquip(in AbilityContext ctx, AbilityInstance self) { equips++; }
            public void OnUnequip(in AbilityContext ctx, AbilityInstance self) { unequips++; }
        }

        private static AbilityContext Ctx()
            => new AbilityContext(null, Vector3.zero, null, new AbilityModifiers(), null, new CombatStats());

        [Test]
        public void EquipAll_CallsOnEquipForLifecycle()
        {
            var loadout = new AbilityLoadout();
            var a = ScriptableObject.CreateInstance<LifeAbility>();
            loadout.TryAdd(a);
            var runner = new AbilityRunner(loadout, Ctx());
            runner.EquipAll();
            Assert.AreEqual(1, ((LifeAbility)loadout.Actives[0].data).equips);
        }

        [Test]
        public void Unequip_CallsOnUnequipForLifecycle()
        {
            var loadout = new AbilityLoadout();
            var a = ScriptableObject.CreateInstance<LifeAbility>();
            loadout.TryAdd(a);
            var inst = loadout.Actives[0];
            var runner = new AbilityRunner(loadout, Ctx());
            runner.Unequip(inst);
            Assert.AreEqual(1, ((LifeAbility)inst.data).unequips);
        }

        private readonly List<GameObject> created = new List<GameObject>();

        /// <summary> 테스트에서 생성한 GameObject를 정리합니다. </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            created.Clear();
        }

        private sealed class BasicFiring : ActiveAbilityData
        {
            public int fires;

            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target)
            {
                fires++;
            }
        }

        private sealed class SignatureFiring : ActiveAbilityData
        {
            public int fires;

            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target)
            {
                fires++;
            }
        }

        private sealed class PersistentFiring : ActiveAbilityData, IAbilityLifecycle
        {
            public int fires;

            protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target)
            {
                fires++;
            }

            public void OnEquip(in AbilityContext ctx, AbilityInstance self)
            {
            }

            public void OnUnequip(in AbilityContext ctx, AbilityInstance self)
            {
            }
        }

        private static BasicFiring MakeBasicFiring()
        {
            BasicFiring a = ScriptableObject.CreateInstance<BasicFiring>();
            a.tier = AbilityTier.Basic;
            return a;
        }

        private static SignatureFiring MakeSignatureFiring(float cooldown)
        {
            SignatureFiring a = ScriptableObject.CreateInstance<SignatureFiring>();
            a.tier = AbilityTier.Signature;
            a.baseCooldown = cooldown;
            return a;
        }

        private static PersistentFiring MakePersistentFiring()
        {
            PersistentFiring a = ScriptableObject.CreateInstance<PersistentFiring>();
            a.tier = AbilityTier.Signature;
            return a;
        }

        private static int FireCountOf(AbilityData data)
        {
            if (data is BasicFiring basic)
            {
                return basic.fires;
            }

            if (data is SignatureFiring signature)
            {
                return signature.fires;
            }

            if (data is PersistentFiring persistent)
            {
                return persistent.fires;
            }

            return -1;
        }

        /// <summary> 사거리 내 활성 적 1체를 등록한 실제 타겟팅 컨텍스트를 만듭니다. </summary>
        private AbilityContext MakeCtx()
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
            return new AbilityContext(null, Vector3.zero, finder, new AbilityModifiers(), null, new CombatStats());
        }

        [Test]
        public void Tick_FiresNonBasic_OnOwnCooldown()
        {
            var loadout = new AbilityLoadout();
            var sub = MakeSignatureFiring(cooldown: 1f);   // tier=Signature, Fire 시 카운트
            loadout.TryAdd(sub);
            var runner = new AbilityRunner(loadout, MakeCtx());
            runner.Tick(0.5f);
            Assert.AreEqual(1, FireCountOf(sub), "비-Basic 능력은 자기 쿨다운으로 발사");

            runner.Tick(0.1f);
            Assert.AreEqual(1, FireCountOf(sub), "쿨다운이 남아있으면 재발사되지 않음");
        }

        [Test]
        public void Tick_SkipsBasicAttack()
        {
            var loadout = new AbilityLoadout();
            var basic = MakeBasicFiring();   // tier=Basic
            loadout.TryAdd(basic);
            var runner = new AbilityRunner(loadout, MakeCtx());
            runner.Tick(5f);
            Assert.AreEqual(0, FireCountOf(basic), "기본 공격은 러너가 발사하지 않음(CoreWeapon 담당)");
        }

        [Test]
        public void Tick_SkipsPersistentAbility_ButFiresNonBasicInMixedLoadout()
        {
            var loadout = new AbilityLoadout();
            var basic = MakeBasicFiring();
            var signature = MakeSignatureFiring(cooldown: 0.5f);
            var persistent = MakePersistentFiring();
            loadout.TryAdd(basic);
            loadout.TryAdd(signature);
            loadout.TryAdd(persistent);

            var runner = new AbilityRunner(loadout, MakeCtx());
            runner.Tick(0.5f);

            Assert.AreEqual(0, FireCountOf(basic), "기본 공격은 러너가 발사하지 않음(CoreWeapon 담당)");
            Assert.AreEqual(1, FireCountOf(signature), "비-Basic 액티브는 자율 발사");
            Assert.AreEqual(0, FireCountOf(persistent), "상시 능력(IAbilityLifecycle)은 DriveAutonomously 대상이 아님");
        }
    }
}
