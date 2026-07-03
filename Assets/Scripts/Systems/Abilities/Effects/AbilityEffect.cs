// 자가구동 효과 엔티티 베이스 — 풀 반납은 Dispose(코어 주입)
using DefenseDot.Core.Pooling;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary>
    /// 능력이 스폰하는 자가구동 효과의 베이스입니다.
    /// 시간축 거동·데미지는 서브클래스 Update가 수행하고, 종료 시 ReturnToPool로 반납합니다.
    /// </summary>
    public abstract class AbilityEffect : PooledBehaviour
    {
        private IEffectSpawner spawner;

        /// <summary> 중첩 VFX 스폰용 스포너를 주입합니다(반납 아님). </summary>
        public void Bind(IEffectSpawner effectSpawner) => spawner = effectSpawner;

        /// <summary> 중첩 일회성 VFX 스폰에 쓰는 스포너입니다. </summary>
        protected IEffectSpawner Spawner => spawner;

        /// <summary> 효과를 풀로 반납합니다. </summary>
        protected void ReturnToPool() => Dispose();
    }
}
