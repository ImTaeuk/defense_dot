// 전투 사건과 경제 모델을 잇는다 — 자기 데이터는 갖지 않는다
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Economy
{
    /// <summary>
    /// 전투 결과(적 처치)를 골드 보상으로 잇는 바인더입니다. (POCO)
    /// CombatModel의 처치 사건을 구독하여 EconomyModel을 갱신하며, 판정 로직은 갖지 않습니다.
    /// </summary>
    public class EconomyEventBinder
    {
        private readonly EconomyModel economy;
        private readonly CombatModel combat;

        public EconomyEventBinder(EconomyModel economy, CombatModel combat)
        {
            this.economy = economy;
            this.combat = combat;
        }

        /// <summary>
        /// 사건 구독을 시작합니다.
        /// </summary>
        public void Initialize()
        {
            combat.OnEnemyKilled += HandleEnemyKilled;
        }

        /// <summary>
        /// 사건 구독을 해제합니다. (Lapsed Listener 방지)
        /// </summary>
        public void Dispose()
        {
            combat.OnEnemyKilled -= HandleEnemyKilled;
        }

        private void HandleEnemyKilled(int reward)
        {
            economy.AddGold(reward);
        }
    }
}
