using System.Collections.Generic;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 코어가 보유하는 능력 슬롯(액티브/패시브)과 그 관리 API입니다. </summary>
    public sealed class AbilityLoadout
    {
        private readonly int activeCapacity;
        private readonly int passiveCapacity;
        private readonly List<AbilityInstance> actives = new List<AbilityInstance>();
        private readonly List<AbilityInstance> passives = new List<AbilityInstance>();
        private readonly AbilityModifiers modifiers = new AbilityModifiers();

        /// <summary> 장착된 액티브 능력들. </summary>
        public IReadOnlyList<AbilityInstance> Actives => actives;
        /// <summary> 장착된 패시브 능력들. </summary>
        public IReadOnlyList<AbilityInstance> Passives => passives;
        /// <summary> 패시브 합산 보정(캐시). </summary>
        public AbilityModifiers Modifiers => modifiers;

        public AbilityLoadout(int activeCapacity = 6, int passiveCapacity = 6)
        {
            this.activeCapacity = activeCapacity;
            this.passiveCapacity = passiveCapacity;
        }

        /// <summary> 보유 여부(액티브·패시브 통합). </summary>
        public bool Contains(AbilityData data)
        {
            for (int i = 0; i < actives.Count; i++) if (actives[i].data == data) return true;
            for (int i = 0; i < passives.Count; i++) if (passives[i].data == data) return true;
            return false;
        }

        /// <summary> 추가 가능 여부(타입별 슬롯 여유 + 미보유). </summary>
        public bool CanAdd(AbilityData data)
        {
            if (data == null || Contains(data)) return false;
            if (data is PassiveAbilityData) return passives.Count < passiveCapacity;
            return actives.Count < activeCapacity;   // 그 외(ActiveAbilityData)
        }

        /// <summary> 새 능력을 해당 슬롯에 추가합니다. 불가 시 false. </summary>
        public bool TryAdd(AbilityData data)
        {
            if (!CanAdd(data)) return false;
            var inst = new AbilityInstance(data, 1);
            if (data is PassiveAbilityData)
            {
                passives.Add(inst);
                RecalculateModifiers();
            }
            else
            {
                actives.Add(inst);
            }
            return true;
        }

        /// <summary> 레벨업(maxLevel 클램프). 패시브면 보정 재계산. </summary>
        public void LevelUp(AbilityInstance inst)
        {
            if (inst == null || inst.level >= inst.data.maxLevel) return;
            inst.level++;
            if (inst.data is PassiveAbilityData) RecalculateModifiers();
        }

        /// <summary> 제거. 패시브면 보정 재계산. </summary>
        public void Remove(AbilityInstance inst)
        {
            if (inst == null) return;
            if (passives.Remove(inst)) RecalculateModifiers();
            else actives.Remove(inst);
        }

        private void RecalculateModifiers()
        {
            modifiers.damageBonus = 0f;
            modifiers.cooldownReduction = 0f;
            for (int i = 0; i < passives.Count; i++)
            {
                var p = passives[i].data as PassiveAbilityData;
                if (p != null) p.ApplyModifiers(modifiers, passives[i].level);
            }
        }
    }
}
