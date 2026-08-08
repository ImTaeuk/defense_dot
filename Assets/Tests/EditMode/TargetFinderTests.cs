using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Enemy;
using DefenseDot.Systems.Tower;

public class TargetFinderTests
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
}
