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
        /// <summary> 로드아웃 구조 변경(추가/레벨업/제거) 후 발화합니다. </summary>
        public event System.Action OnChanged;

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

        /// <summary> 기본 공격(Basic)을 이미 보유했는지 여부입니다. </summary>
        public bool HasBasicAttack()
        {
            for (int i = 0; i < actives.Count; i++)
            {
                if (actives[i].data.tier == AbilityTier.Basic)
                    return true;
            }
            return false;
        }

        /// <summary> 추가 가능 여부(타입별 슬롯 여유 + 미보유 + 주축 중복 금지). </summary>
        /// <param name="data">추가하려는 능력 설계도</param>
        public bool CanAdd(AbilityData data)
        {
            if (data == null || Contains(data))
                return false;
            if (data is PassiveAbilityData)
                return passives.Count < passiveCapacity;

            // 기본 공격(Basic)은 1개만 — 교체는 합성 승계로만 일어난다
            if (data.tier == AbilityTier.Basic && HasBasicAttack())
                return false;

            return actives.Count < activeCapacity;   // 그 외(ActiveAbilityData)
        }

        /// <summary> 새 능력을 해당 슬롯에 추가합니다. 획득 라운드를 박제하고 통지합니다. 불가 시 false. </summary>
        public bool TryAdd(AbilityData data)
        {
            if (!CanAdd(data)) return false;

            var inst = new AbilityInstance(data, 1);
            inst.acquiredRound = System.Math.Max(1, modifiers.combatState != null ? modifiers.combatState.Round : 1);

            if (data is PassiveAbilityData)
            {
                passives.Add(inst);
                RecalculateModifiers();
            }
            else
            {
                actives.Add(inst);
            }

            OnChanged?.Invoke();
            return true;
        }

        /// <summary> 레벨업(maxLevel 클램프). 패시브면 보정 재계산 후 통지. </summary>
        public void LevelUp(AbilityInstance inst)
        {
            if (inst == null || inst.level >= inst.data.maxLevel) return;
            inst.level++;
            if (inst.data is PassiveAbilityData) RecalculateModifiers();
            OnChanged?.Invoke();
        }

        /// <summary> 제거. 패시브면 보정 재계산. 실제 제거 시 통지. </summary>
        public void Remove(AbilityInstance inst)
        {
            if (inst == null) return;

            bool removed;
            if (passives.Remove(inst))
            {
                RecalculateModifiers();
                removed = true;
            }
            else
            {
                removed = actives.Remove(inst);
            }

            if (removed) OnChanged?.Invoke();
        }

        private void RecalculateModifiers()
        {
            modifiers.ResetAccumulated();
            for (int i = 0; i < passives.Count; i++)
            {
                var p = passives[i].data as PassiveAbilityData;
                if (p != null) p.ApplyModifiers(modifiers, passives[i].level);
            }
        }
    }
}
