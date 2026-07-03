using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 이펙트 스폰 시임 — 엔티티 풀 대여와 일회성 VFX 재생을 제공합니다. </summary>
    public interface IEffectSpawner
    {
        /// <summary> 풀에서 효과 엔티티를 꺼내 스포너를 배선해 돌려줍니다. </summary>
        T Spawn<T>(AssetReferenceGameObject asset) where T : AbilityEffect;

        /// <summary> 일회성 VFX를 꺼내 위치잡고 재생 — 수명 뒤 자동 반납합니다. </summary>
        void PlayOneShot(AssetReferenceGameObject asset, Vector3 pos, Quaternion rot);

        /// <summary> 효과를 풀로 반납합니다. </summary>
        void Release(AbilityEffect fx);
    }
}
