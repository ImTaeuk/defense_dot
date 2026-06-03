// 경제 컨트롤러 — 적 처치 사건을 골드 보상으로 연결
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Economy
{
    /// <summary>
    /// 전투 결과(적 처치)를 골드 보상으로 연결하는 컨트롤러입니다. (POCO)
    /// CombatModel의 처치 사건을 구독하여 EconomyModel을 갱신합니다.
    /// </summary>
    public class EconomyController
    {
        private readonly EconomyModel economy;
        private readonly CombatModel combat;

        public EconomyController(EconomyModel economy, CombatModel combat)
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
