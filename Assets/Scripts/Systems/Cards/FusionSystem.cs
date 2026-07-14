using System.Collections.Generic;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 합성(Fusion)의 단일 원천 — 계보 데이터 소유 + 판정·생성·배제·원자적 적용. </summary>
    public sealed class FusionSystem
    {
        /// <summary> 이 시스템이 직접 소유하는 합성 계보(데이터 원천). </summary>
        private readonly FusionRecipeSet lineage;

        /// <summary> 계보를 주입받습니다(null이면 합성 비활성). </summary>
        /// <param name="lineage">이 타워의 합성 계보 데이터.</param>
        public FusionSystem(FusionRecipeSet lineage)
        {
            this.lineage = lineage;
        }

        /// <summary> 지금 가능한 합성을 카드로 만들어 목록에 채웁니다. </summary>
        /// <param name="loadout">재료 보유·MAX 판정에 쓰는 현재 보유 능력.</param>
        /// <param name="into">합성 카드를 추가할 대상 목록.</param>
        /// <param name="max">into가 도달하면 멈출 카드 총 개수 한도.</param>
        public void CollectOffers(AbilityLoadout loadout, List<Card> into, int max)
        {
            if (loadout == null || into == null || lineage == null || lineage.recipes == null)
                return;

            for (int i = 0; i < lineage.recipes.Count && into.Count < max; i++)
            {
                FusionRecipe r = lineage.recipes[i];

                // 참조 누락 레시피는 건너뜀
                if (r.materialA == null || r.materialB == null || r.result == null)
                    continue;

                // 결과를 이미 보유하면 제시하지 않음
                if (loadout.Contains(r.result))
                    continue;

                // 재료 둘 다 MAX 보유해야 가용
                if (FindMaxed(loadout, r.materialA) == null || FindMaxed(loadout, r.materialB) == null)
                    continue;

                into.Add(Card.FusionCard(r.result, r.materialA, r.materialB, CardTier.Fusion));
            }
        }

        /// <summary> 해당 능력이 계보의 결과인지 검사합니다(일반 풀 배제용). </summary>
        /// <param name="data">검사할 능력 설계도.</param>
        public bool IsResult(AbilityData data)
        {
            if (lineage == null || lineage.recipes == null)
                return false;

            for (int i = 0; i < lineage.recipes.Count; i++)
            {
                if (lineage.recipes[i].result == data)
                    return true;
            }

            return false;
        }

        /// <summary> 합성 카드를 재검증 후 원자적으로 적용합니다(재료 2개 소진 → 결과 부여). </summary>
        /// <param name="core">능력 추가/삭제 명령 대상(코어).</param>
        /// <param name="card">적용할 합성 카드(재료·결과 식별자 보유).</param>
        public void Apply(IAbilityCommandTarget core, Card card)
        {
            if (core == null || card.data == null)
                return;

            AbilityLoadout loadout = core.Loadout;

            // 1. 적용 직전 재검증(예열 대기 사이 상태 변동 방어)
            AbilityInstance a = FindMaxed(loadout, card.materialA);
            AbilityInstance b = FindMaxed(loadout, card.materialB);
            if (a == null || b == null || loadout.Contains(card.data))
                return;

            // 2. 재료 소진 → 결과 부여(여기까지 await 없음 = 원자적)
            core.RemoveAbility(a);
            core.RemoveAbility(b);
            core.AddAbility(card.data);
        }

        /// <summary> 로드아웃에서 해당 능력의 MAX 인스턴스를 찾습니다(없거나 비MAX면 null). </summary>
        /// <param name="loadout">탐색 대상 로드아웃.</param>
        /// <param name="data">찾을 능력 설계도.</param>
        private static AbilityInstance FindMaxed(AbilityLoadout loadout, AbilityData data)
        {
            AbilityInstance inst = Find(loadout.Actives, data);
            if (inst == null)
                inst = Find(loadout.Passives, data);

            if (inst == null || inst.level < inst.data.maxLevel)
                return null;

            return inst;
        }

        /// <summary> 목록에서 설계도 일치 인스턴스를 찾습니다. </summary>
        /// <param name="list">탐색할 인스턴스 목록.</param>
        /// <param name="data">찾을 능력 설계도.</param>
        private static AbilityInstance Find(IReadOnlyList<AbilityInstance> list, AbilityData data)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].data == data)
                    return list[i];
            }

            return null;
        }
    }
}
