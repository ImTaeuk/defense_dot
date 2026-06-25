// A2 임시 스포너 — Instantiate/Destroy. 풀링은 TASK-013에서 교체.
using UnityEngine;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 풀링 없이 Instantiate/Destroy로 동작하는 임시 스포너입니다. </summary>
    public sealed class SimpleEffectSpawner : IEffectSpawner
    {
        private readonly Transform container;

        public SimpleEffectSpawner(Transform container = null) { this.container = container; }

        public T Spawn<T>(T prefab) where T : AbilityEffect
        {
            T fx = Object.Instantiate(prefab, container);
            fx.Bind(this);
            fx.OnSpawn();
            return fx;
        }

        public void Release(AbilityEffect fx)
        {
            if (fx == null) return;
            fx.OnDespawn();
            Object.Destroy(fx.gameObject);
        }
    }
}
