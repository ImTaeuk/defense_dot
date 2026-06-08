// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DefenseDot.Core;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary> 코드 생성 디버그 투사체. 명중 시 데미지 후 다음 최근접 적으로 관통, 수명 종료 시 파괴. (DEBUG) </summary>
    public class DebugProjectile : MonoBehaviour
    {
        private TargetFinder finder;
        private ITargetable target;
        private float damage;
        private float speed;
        private float range;
        private int pierceRemaining;
        private float life;
        private readonly HashSet<ITargetable> hit = new HashSet<ITargetable>();

        /// <summary> 디버그 투사체를 생성해 발사합니다. </summary>
        public static void Spawn(Vector3 origin, ITargetable target, TargetFinder finder, float damage, float speed, float range, int pierce)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DebugProjectile";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.25f;
            if (target != null) go.transform.forward = (target.Position - origin).normalized;
            Object.Destroy(go.GetComponent<Collider>());

            DebugProjectile p = go.AddComponent<DebugProjectile>();
            p.finder = finder;
            p.target = target;
            p.damage = damage;
            p.speed = speed;
            p.range = range;
            p.pierceRemaining = pierce;
            p.life = 3f;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            // 타겟 소실 시 직진하다 수명 종료
            if (target == null || !target.IsActive)
            {
                transform.position += transform.forward * (speed * Time.deltaTime);
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, target.Position, speed * Time.deltaTime);
            if ((transform.position - target.Position).sqrMagnitude >= 0.09f) return;

            // 명중 → 데미지 후 다음 최근접 미명중 적으로 관통
            if (target is IDamageable damageable) damageable.TakeDamage(damage);
            hit.Add(target);
            pierceRemaining--;
            if (pierceRemaining <= 0) { Destroy(gameObject); return; }
            target = NextNearestUnhit();
        }

        /// <summary> 사거리 내 미명중 최근접 적을 반환합니다. </summary>
        private ITargetable NextNearestUnhit()
        {
            List<ITargetable> cands = ListPool<ITargetable>.Get();
            finder.FindAllInRange(transform.position, range, cands);
            ITargetable best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < cands.Count; i++)
            {
                ITargetable c = cands[i];
                if (hit.Contains(c)) continue;
                float d = (c.Position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            ListPool<ITargetable>.Release(cands);
            return best;
        }
    }
}
