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
                int bonusLevels = RollBonusLevels(config);
                CardTier tier = bonusLevels >= 2 ? CardTier.SuperLucky
                    : bonusLevels == 1 ? CardTier.Lucky
                    : (pickNew ? CardTier.New : CardTier.Upgrade);
                if (pickNew)
                {
                    int idx = Index(newPool.Count);
                    AbilityData picked = newPool[idx];
                    int toLevel = UnityEngine.Mathf.Min(picked.maxLevel, 1 + bonusLevels);
                    result.Add(CardChoice.NewCard(picked, tier, toLevel));
                    newPool.RemoveAt(idx);
                }
                else
                {
                    int idx = Index(levelPool.Count);
                    AbilityInstance inst = levelPool[idx];
                    int toLevel = UnityEngine.Mathf.Min(inst.data.maxLevel, inst.level + 1 + bonusLevels);
                    result.Add(CardChoice.LevelCard(inst, tier, toLevel));
                    levelPool.RemoveAt(idx);
                }
            }
            return result;
        }

        /// <summary> 럭키 굴림 — 보너스 레벨 수 반환(0 일반 / 1 럭키 / 2 슈퍼럭키). </summary>
        private int RollBonusLevels(ArenaCardConfig config)
        {
            if (!config.enableLucky) return 0;
            float roll = rng();
            if (roll < config.superLuckyChance) return 2;
            if (roll < config.superLuckyChance + config.luckyChance) return 1;
            return 0;
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
