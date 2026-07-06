using Cysharp.Threading.Tasks;
using DefenseDot.Core.Pooling;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 선택된 카드를 능력 대상에 적용하는 뷰 비의존 순수 로직. </summary>
    public static class CardChoiceApplier
    {
        /// <summary> 카드 선택을 반영합니다. 신규면 이펙트를 예열한 뒤 추가하고, 목표 레벨까지 레벨업합니다. </summary>
        public static async UniTask ApplyAsync(ICardCommandTarget core, CardChoice choice, PoolManager pool)
        {
            if (choice.action == CardAction.New)
            {
                if (pool != null && choice.data != null) await pool.WarmupAsync(choice.data.EffectAssets);
                AbilityInstance added = core.AddAbility(choice.data);
                if (added != null)
                    for (int lv = added.level; lv < choice.toLevel; lv++) core.LevelUpAbility(added);
            }
            else
            {
                for (int lv = choice.fromLevel; lv < choice.toLevel; lv++) core.LevelUpAbility(choice.instance);
            }
        }
    }
}
