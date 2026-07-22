namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 자율 발동 능력(추상). 무기의 발사 주기와 무관하게 자기 쿨다운으로 동작합니다.
    /// </summary>
    public abstract class AutoAbilityData : ActiveAbilityData
    {
        /// <summary> 매 프레임 구동 — 쿨다운이 차고 타겟이 있으면 발사합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="deltaTime">경과 시간(초)</param>
        public virtual void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime)
        {
            DriveAutonomously(ctx, self, deltaTime);
        }
    }
}
