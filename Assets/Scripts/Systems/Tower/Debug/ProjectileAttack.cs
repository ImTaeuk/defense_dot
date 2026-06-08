// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary> 최근접 적을 향해 디버그 투사체를 발사. (DEBUG) </summary>
    public class ProjectileAttack : IAttackBehavior
    {
        private const float Speed = 12f;
        private const int PierceMax = 3;

        public void Execute(in AttackContext ctx)
        {
            if (ctx.Finder == null || ctx.Data == null || ctx.Host == null) return;
            ITargetable target = ctx.Finder.FindNearest(ctx.Origin, ctx.Data.attackRange);
            if (target == null) return;
            DebugProjectile.Spawn(ctx.Origin, target, ctx.Finder, ctx.Data.attackDamage, Speed, ctx.Data.attackRange, PierceMax);
        }
    }
}
