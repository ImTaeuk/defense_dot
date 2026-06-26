using System.Collections.Generic;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 레벨업 시 보유/슬롯/풀을 보고 카드 N장을 생성. </summary>
    public sealed class CardChoiceGenerator
    {
        private readonly System.Func<float> rng;

        public CardChoiceGenerator(System.Func<float> rng = null)
        {
            this.rng = rng ?? (() => UnityEngine.Random.value);
        }

        public List<CardChoice> Generate(AbilityLoadout loadout, AbilityPool pool, ArenaCardConfig config, int level)
        {
            var result = new List<CardChoice>();
            if (loadout == null || config == null) return result;

            var newPool = new List<AbilityData>();
            if (pool != null)
            {
                for (int i = 0; i < pool.abilities.Count; i++)
                {
                    var d = pool.abilities[i];
                    if (d != null && loadout.CanAdd(d)) newPool.Add(d); // 슬롯+미보유 동시 검사
                }
            }

            var levelPool = new List<AbilityInstance>();
            CollectLevelable(loadout.Actives, levelPool);
            CollectLevelable(loadout.Passives, levelPool);

            float newChance = level < config.earlyLevelThreshold
                ? config.newCardChanceEarly : config.newCardChanceLate;

            for (int n = 0; n < config.choiceCount; n++)
            {
                bool canNew = newPool.Count > 0;
                bool canLv = levelPool.Count > 0;
                if (!canNew && !canLv) break;

                bool pickNew = canNew && (!canLv || rng() < newChance);
                if (pickNew)
                {
                    int idx = Index(newPool.Count);
                    result.Add(CardChoice.NewCard(newPool[idx]));
                    newPool.RemoveAt(idx);
                }
                else
                {
                    int idx = Index(levelPool.Count);
                    result.Add(CardChoice.LevelCard(levelPool[idx]));
                    levelPool.RemoveAt(idx);
                }
            }
            return result;
        }

        private int Index(int count)
        {
            int idx = (int)(rng() * count);
            return idx >= count ? count - 1 : idx;
        }

        private static void CollectLevelable(IReadOnlyList<AbilityInstance> src, List<AbilityInstance> dst)
        {
            for (int i = 0; i < src.Count; i++)
                if (src[i].level < src[i].data.maxLevel) dst.Add(src[i]);
        }
    }
}
