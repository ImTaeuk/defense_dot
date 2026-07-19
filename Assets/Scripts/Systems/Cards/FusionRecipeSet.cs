using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> 한 타워(캐릭터)의 합성 계보 — 재료 2개→결과 레시피 목록입니다. (데이터만) </summary>
    [CreateAssetMenu(fileName = "FusionRecipeSet", menuName = "DefenseDot/Fusion Recipe Set")]
    public sealed class FusionRecipeSet : ScriptableObject
    {
        /// <summary> 합성 레시피 목록. </summary>
        public List<FusionRecipe> recipes = new List<FusionRecipe>();

        /// <summary> 디자이너 실수(null·자기합성·결과=재료·주축 상실)를 콘솔 경고로 알립니다. </summary>
        private void OnValidate()
        {
            if (recipes == null)
                return;
            for (int i = 0; i < recipes.Count; i++)
            {
                FusionRecipe r = recipes[i];
                if (r.materialA == null || r.materialB == null || r.result == null)
                    Debug.LogWarning($"[FusionRecipeSet] {name} 레시피 {i}: 참조 누락", this);
                else if (r.materialA == r.materialB)
                    Debug.LogWarning($"[FusionRecipeSet] {name} 레시피 {i}: 재료 A==B", this);
                else if (r.result == r.materialA || r.result == r.materialB)
                    Debug.LogWarning($"[FusionRecipeSet] {name} 레시피 {i}: 결과가 재료와 같음", this);
                else if (!r.KeepsMainWeapon())
                    Debug.LogWarning($"[FusionRecipeSet] {name} 레시피 {i}: 주축을 재료로 쓰는데 결과가 주축이 아님 — 합성 시 주 공격을 잃습니다", this);
            }
        }
    }

    /// <summary> 합성 레시피 1건 — 재료 2개 소진 → 결과 1개. </summary>
    [System.Serializable]
    public struct FusionRecipe
    {
        /// <summary> 재료 A. </summary>
        public AbilityData materialA;
        /// <summary> 재료 B. </summary>
        public AbilityData materialB;
        /// <summary> 결과 능력(일반 카드 풀 제외). </summary>
        public AbilityData result;

        /// <summary> 이 레시피가 합성 후에도 주 공격을 남기는지 검사합니다. </summary>
        public bool KeepsMainWeapon() => KeepsMainWeapon(materialA, materialB, result);

        /// <summary> 주축을 재료로 소진하면 결과도 주축이어야 합니다(합성 후 주 공격 상실 방지). </summary>
        /// <param name="materialA">재료 A</param>
        /// <param name="materialB">재료 B</param>
        /// <param name="result">결과 능력</param>
        public static bool KeepsMainWeapon(AbilityData materialA, AbilityData materialB, AbilityData result)
        {
            bool consumesMain = materialA is MainAbilityData || materialB is MainAbilityData;
            if (!consumesMain)
                return true;

            return result is MainAbilityData;
        }
    }
}
