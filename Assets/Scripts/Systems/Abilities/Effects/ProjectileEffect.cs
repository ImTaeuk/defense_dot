// 유도 투사체 효과 — 명중 데미지 후 미명중 최근접으로 관통, 수명/관통 소진 시 반납
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DefenseDot.Core;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 능력이 발사하는 유도 투사체 효과입니다. </summary>
    public sealed class ProjectileEffect : AbilityEffect
    {
        [SerializeField] private GameObject hitVfxPrefab;   // 명중 VFX(Hovl Hit)

        private TargetFinder finder;
        private ITargetable target;
        private DamageSource source;
        private float speed;
        private float range;
        private int pierceRemaining;
        private float life;
        private readonly HashSet<ITargetable> hit = new HashSet<ITargetable>();

        /// <summary> 투사체를 활성화합니다. </summary>
        public void Activate(Vector3 origin, ITargetable target, DamageSource source, float speed, int pierce, float range, TargetFinder finder)
        {
            transform.position = origin;
            this.target = target;
            this.source = source;
            this.speed = speed;
            this.pierceRemaining = Mathf.Max(1, pierce);
            this.range = range;
            this.finder = finder;
            this.life = 3f;
            hit.Clear();
            if (target != null) transform.forward = (target.Position - origin).normalized;
            PlayVisuals();
        }

        /// <summary> playOnAwake 비주얼 파티클을 명시 재생합니다. (스폰 방식·재발사와 무관하게 보장) </summary>
        private void PlayVisuals()
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                if (systems[i].main.playOnAwake) { systems[i].Clear(true); systems[i].Play(true); }
        }

        public override void OnDespawn() { hit.Clear(); target = null; }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Release(); return; }

            if (target == null || !target.IsActive)
            {
                transform.position += transform.forward * (speed * Time.deltaTime);
                return;
            }

            Vector3 toTarget = target.Position - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f) transform.forward = toTarget.normalized;   // 진행 방향을 바라봄
            transform.position = Vector3.MoveTowards(transform.position, target.Position, speed * Time.deltaTime);
            if ((transform.position - target.Position).sqrMagnitude >= 0.09f) return;

            if (hitVfxPrefab != null)
                VfxPlayer.SpawnOneShot(hitVfxPrefab, transform.position, Quaternion.identity);
            if (target is IDamageable damageable) damageable.TakeDamage(source.Resolve(target));
            hit.Add(target);
            pierceRemaining--;
            if (pierceRemaining <= 0) { Release(); return; }
            target = NextNearestUnhit();
        }

        private ITargetable NextNearestUnhit()
        {
            if (finder == null) return null;
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
