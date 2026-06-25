// 능력 러너 — 매 프레임 액티브 Tick, 장착/해제 시 라이프사이클 호출
using System.Collections.Generic;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 로드아웃의 액티브 능력을 매 프레임 구동하고, 장착/해제 시
    /// IAbilityLifecycle 훅을 호출하는 순수 C# 러너입니다.
    /// </summary>
    public sealed class AbilityRunner
    {
        private readonly AbilityLoadout loadout;
        private readonly AbilityContext ctx;

        public AbilityRunner(AbilityLoadout loadout, in AbilityContext ctx)
        {
            this.loadout = loadout;
            this.ctx = ctx;
        }

        /// <summary> 현재 장착된 모든 액티브에 OnEquip을 적용합니다. </summary>
        public void EquipAll()
        {
            IReadOnlyList<AbilityInstance> actives = loadout.Actives;
            for (int i = 0; i < actives.Count; i++) Equip(actives[i]);
        }

        /// <summary> 한 능력의 OnEquip을 호출합니다(라이프사이클 보유 시). </summary>
        public void Equip(AbilityInstance inst)
        {
            if (inst != null && inst.data is IAbilityLifecycle life) life.OnEquip(ctx, inst);
        }

        /// <summary> 한 능력의 OnUnequip을 호출합니다(라이프사이클 보유 시). </summary>
        public void Unequip(AbilityInstance inst)
        {
            if (inst != null && inst.data is IAbilityLifecycle life) life.OnUnequip(ctx, inst);
        }

        /// <summary> 매 프레임 모든 액티브를 Tick합니다. </summary>
        public void Tick(float deltaTime)
        {
            IReadOnlyList<AbilityInstance> actives = loadout.Actives;
            for (int i = 0; i < actives.Count; i++)
            {
                AbilityInstance inst = actives[i];
                if (inst.data is ActiveAbilityData active) active.Tick(ctx, inst, deltaTime);
            }
        }
    }
}
