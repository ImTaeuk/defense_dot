using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core.Pooling;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> PoolManager를 감싸 효과 엔티티·일회성 VFX를 풀링으로 제공합니다. </summary>
    public sealed class PooledEffectSpawner : IEffectSpawner
    {
        private readonly PoolManager pool;

        public PooledEffectSpawner(PoolManager poolManager) { pool = poolManager; }

        /// <summary> 효과 엔티티를 꺼내 스포너를 배선합니다. 실패 시 null. </summary>
        public T Spawn<T>(AssetReferenceGameObject asset) where T : AbilityEffect
        {
            if (!pool.TryGet<T>(asset, out T fx)) return null;
            fx.Bind(this);
            return fx;
        }

        /// <summary> 일회성 VFX를 꺼내 위치잡고 재생 후 자동 반납합니다. 실패 시 스킵. </summary>
        public void PlayOneShot(AssetReferenceGameObject asset, Vector3 pos, Quaternion rot)
        {
            if (!pool.TryGet<VfxPlayer>(asset, out VfxPlayer vp)) return;
            vp.transform.SetPositionAndRotation(pos, rot);
            vp.PlayThenReturn();
        }

        /// <summary> 효과를 풀로 반납합니다. </summary>
        public void Release(AbilityEffect fx) { fx?.Dispose(); }
    }
}
