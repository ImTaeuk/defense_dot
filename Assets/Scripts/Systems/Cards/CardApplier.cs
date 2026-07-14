using Cysharp.Threading.Tasks;
using DefenseDot.Core.Pooling;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 선택된 카드를 능력 대상에 적용하는 뷰 비의존 순수 로직. </summary>
    public static class CardApplier
    {
        /// <summary> 카드 선택을 반영합니다. 신규/합성은 이펙트를 예열한 뒤 적용하고, 레벨 카드는 목표 레벨까지 올립니다. </summary>
        public static async UniTask ApplyAsync(IAbilityCommandTarget core, Card card, PoolManager pool, FusionSystem fusion = null)
        {
            if (card.applyType == CardApplyType.New)
            {
                if (pool != null && card.data != null) await pool.WarmupAsync(card.data.EffectAssets);
                AbilityInstance added = core.AddAbility(card.data);
                if (added != null)
                    for (int lv = added.level; lv < card.toLevel; lv++) core.LevelUpAbility(added);
            }
            else if (card.applyType == CardApplyType.Fuse)
            {
                // 예열(유일한 await)을 먼저 → 합성 적용은 FusionSystem의 원자적 연산에 위임
                if (pool != null && card.data != null) await pool.WarmupAsync(card.data.EffectAssets);
                fusion?.Apply(core, card);
            }
            else
            {
                for (int lv = card.fromLevel; lv < card.toLevel; lv++) core.LevelUpAbility(card.instance);
            }
        }
    }
}
