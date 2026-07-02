using UnityEngine.AddressableAssets;

namespace DefenseDot.Core.Pooling
{
    /// <summary> 이펙트 용도 분류입니다. </summary>
    public enum EffectType
    {
        Hit,
        Muzzle,
        Cast,
        Death
    }

    /// <summary> 이펙트 용도 → Addressables 프리팹 약한참조 매핑입니다. </summary>
    [System.Serializable]
    public struct EffectEntry
    {
        public EffectType type;
        public AssetReferenceGameObject asset;
    }
}
