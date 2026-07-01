using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DefenseDot.Systems.Assets
{
    /// <summary> Addressables 프리팹을 로드·해제하고 핸들을 추적하는 로더입니다. </summary>
    public sealed class AssetLoader
    {
        // GUID 기준 중복 로드 방지
        private readonly Dictionary<object, AsyncOperationHandle<GameObject>> handles
            = new Dictionary<object, AsyncOperationHandle<GameObject>>();

        /// <summary> 참조를 로드해 프리팹을 반환합니다. 이미 로드됐으면 캐시를 반환합니다. </summary>
        public async UniTask<GameObject> LoadAsync(AssetReferenceGameObject reference)
        {
            object key = reference.RuntimeKey;
            if (handles.TryGetValue(key, out AsyncOperationHandle<GameObject> cached))
                return await cached.ToUniTask();

            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(reference);
            handles[key] = handle;
            return await handle.ToUniTask();
        }

        /// <summary> 특정 참조의 핸들을 해제합니다. </summary>
        public void Release(AssetReferenceGameObject reference)
        {
            object key = reference.RuntimeKey;
            if (!handles.TryGetValue(key, out AsyncOperationHandle<GameObject> handle)) return;
            Addressables.Release(handle);
            handles.Remove(key);
        }

        /// <summary> 추적 중인 모든 핸들을 해제합니다. (런/씬 종료 시) </summary>
        public void ReleaseAll()
        {
            foreach (AsyncOperationHandle<GameObject> handle in handles.Values)
                Addressables.Release(handle);
            handles.Clear();
        }
    }
}
