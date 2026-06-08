using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Tower;
using DefenseDot.Systems.Tower.Debugging;

public class AttackBehaviorTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
            if (go != null) Object.DestroyImmediate(go);
        created.Clear();
    }

    private MonsterActor MakeEnemy(EnemyRegistry reg, Vector3 pos, float health)
    {
        GameObject go = new GameObject("TestEnemy");
        go.transform.position = pos;
        created.Add(go);
        MonsterActor actor = go.AddComponent<MonsterActor>();
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.health = health;
        actor.Initialize(data);
        reg.Register(actor);
        return actor;
    }

    [Test]
    public void FindAllInRange_ReturnsOnlyEnemiesWithinRadius()
    {
        EnemyRegistry reg = new EnemyRegistry();
        MonsterActor inside  = MakeEnemy(reg, new Vector3(1f, 0f, 0f), 10f);
        MonsterActor edge    = MakeEnemy(reg, new Vector3(3f, 0f, 0f), 10f);
        MonsterActor outside = MakeEnemy(reg, new Vector3(5f, 0f, 0f), 10f);
        TargetFinder finder = new TargetFinder(reg);

        List<ITargetable> results = new List<ITargetable>();
        finder.FindAllInRange(Vector3.zero, 3f, results);

        Assert.Contains(inside, results);
        Assert.Contains(edge, results);
        Assert.IsFalse(results.Contains(outside));
        Assert.AreEqual(2, results.Count);
    }

    [Test]
    public void AoeAttack_KillsEnemiesInRange_SparesOutside()
    {
        EnemyRegistry reg = new EnemyRegistry();
        MonsterActor inside  = MakeEnemy(reg, new Vector3(1f, 0f, 0f), 1f);
        MonsterActor outside = MakeEnemy(reg, new Vector3(5f, 0f, 0f), 1f);
        TargetFinder finder = new TargetFinder(reg);

        TowerData data = ScriptableObject.CreateInstance<TowerData>();
        data.attackDamage = 5f;
        data.attackRange = 3f;
        AttackContext ctx = new AttackContext(null, Vector3.zero, finder, data);

        new AoeAttack().Execute(in ctx);

        Assert.IsFalse(inside.IsActive,  "범위 내 적은 처치되어야 함");
        Assert.IsTrue(outside.IsActive,  "범위 밖 적은 생존해야 함");
    }
}
