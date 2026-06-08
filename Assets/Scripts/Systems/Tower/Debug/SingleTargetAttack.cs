// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary> 최근접 적 1체에 즉시 데미지 + 타워→타겟 라인. (DEBUG) </summary>
    public class SingleTargetAttack : IAttackBehavior
    {
        public void Execute(in AttackContext ctx)
        {
            if (ctx.Finder == null || ctx.Data == null) return;
            ITargetable target = ctx.Finder.FindNearest(ctx.Origin, ctx.Data.attackRange);
            if (target == null) return;
            if (target is IDamageable damageable) damageable.TakeDamage(ctx.Data.attackDamage);
            float dur = 0.5f / Mathf.Max(0.1f, ctx.Data.attackSpeed);   // 공격 주기 비례
            UnityEngine.Debug.DrawLine(ctx.Origin, target.Position, Color.cyan, dur);
        }
    }
}
