// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary> 최근접 타겟 주변(aoeRadius) 전체 적에 즉시 데미지 + 원 비주얼. (DEBUG) </summary>
    public class AoeAttack : IAttackBehavior
    {
        public void Execute(in AttackContext ctx)
        {
            if (ctx.Finder == null || ctx.Data == null) return;

            // 조준 사거리 내 최근접을 주 타겟으로, 그 주변을 aoeRadius 만큼 폭발
            ITargetable primary = ctx.Finder.FindNearest(ctx.Origin, ctx.Data.attackRange);
            if (primary == null) return;
            Vector3 center = primary.Position;
            float radius = ctx.Data.aoeRadius;

            List<ITargetable> hits = ListPool<ITargetable>.Get();
            ctx.Finder.FindAllInRange(center, radius, hits);
            for (int i = 0; i < hits.Count; i++)
                if (hits[i] is IDamageable damageable) damageable.TakeDamage(ctx.Data.attackDamage);
            ListPool<ITargetable>.Release(hits);

            float dur = 0.5f / Mathf.Max(0.1f, ctx.Data.attackSpeed);   // 공격 주기 비례
            DrawCircle(center, radius, Color.magenta, dur);
        }

        private static void DrawCircle(Vector3 center, float radius, Color color, float duration)
        {
            const int seg = 24;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                UnityEngine.Debug.DrawLine(prev, next, color, duration);
                prev = next;
            }
        }
    }
}
