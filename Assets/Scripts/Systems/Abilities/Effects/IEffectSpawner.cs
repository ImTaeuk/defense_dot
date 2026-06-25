namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 효과 엔티티 스폰·반납 계약입니다. (A2 단순 구현, 풀링은 TASK-013) </summary>
    public interface IEffectSpawner
    {
        /// <summary> 효과 프리팹을 스폰해 반환합니다. </summary>
        T Spawn<T>(T prefab) where T : AbilityEffect;

        /// <summary> 효과를 반납(또는 파괴)합니다. </summary>
        void Release(AbilityEffect fx);
    }
}
