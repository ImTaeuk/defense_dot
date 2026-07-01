using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DefenseDot.Systems.Assets
{
    /// <summary> Addressables 에셋을 로드·해제하고 핸들을 추적하는 로더입니다. </summary>
    public sealed class AssetLoader
    {
        // GUID 기준 중복 로드 방지
        private readonly Dictionary<object, AsyncOperationHandle> handles
            = new Dictionary<object, AsyncOperationHandle>();

        /// <summary> 참조를 로드해 에셋을 반환합니다. 이미 로드됐으면 캐시를 반환합니다. </summary>
        public async UniTask<T> LoadAsync<T>(AssetReference reference) where T : UnityEngine.Object
        {
            object key = reference.RuntimeKey;
            if (handles.TryGetValue(key, out AsyncOperationHandle cached))
                return await cached.Convert<T>().ToUniTask();

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(reference);
            handles[key] = handle;
            return await handle.ToUniTask();
        }

        /// <summary> 특정 참조의 핸들을 해제합니다. </summary>
        public void Release(AssetReference reference)
        {
            object key = reference.RuntimeKey;
            if (!handles.TryGetValue(key, out AsyncOperationHandle handle)) return;
            Addressables.Release(handle);
            handles.Remove(key);
        }

        /// <summary> 추적 중인 모든 핸들을 해제합니다. (런/씬 종료 시) </summary>
        public void ReleaseAll()
        {
            foreach (AsyncOperationHandle handle in handles.Values)
                Addressables.Release(handle);
            handles.Clear();
        }
    }
}
