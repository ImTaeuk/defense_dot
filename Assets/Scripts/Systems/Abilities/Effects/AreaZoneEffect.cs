// 잔류형 범위 존 — duration 동안 반경 내 적을 재타격 쿨다운으로 반복 타격, 범위 비주얼 표시
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 일정 시간 머무르며 반경 내 적을 주기적으로 타격하는 AOE 존입니다. </summary>
    public sealed class AreaZoneEffect : AbilityEffect
    {
        [SerializeField] private GameObject zoneVisualPrefab;   // 범위 비주얼(Hovl AOE/매직서클)
        [SerializeField] private float rehitCooldown = 0.4f;    // 같은 적 재타격 간격
        [SerializeField] private float visualScale = 1f;        // 반경당 비주얼 스케일 보정

        private TargetFinder finder;
        private float radius;
        private DamageSource source;
        private float life;
        private Transform visual;
        private readonly Dictionary<ITargetable, float> rehit = new Dictionary<ITargetable, float>();

        /// <summary> AOE 존을 활성화합니다. </summary>
        public void Activate(Vector3 center, float radius, DamageSource source, float duration, TargetFinder finder)
        {
            transform.position = center;
            this.radius = radius;
            this.source = source;
            this.life = duration;
            this.finder = finder;
            rehit.Clear();
            if (visual == null && zoneVisualPrefab != null)
            {
                GameObject v = Instantiate(zoneVisualPrefab, transform);
                v.transform.localPosition = Vector3.zero;
                visual = v.transform;
                VfxPlayer.EnsurePlay(v, true);
            }
            if (visual != null) visual.localScale = Vector3.one * (radius * visualScale);
        }

        public override void OnDespawn() { rehit.Clear(); }

        private void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            DecayRehit(dt);
            DamageInRadius();
            if (life <= 0f) Release();
        }

        private void DamageInRadius()
        {
            if (finder == null) return;
            List<ITargetable> cands = UnityEngine.Pool.ListPool<ITargetable>.Get();
            finder.FindAllInRange(transform.position, radius, cands);
            for (int i = 0; i < cands.Count; i++)
            {
                ITargetable c = cands[i];
                if (rehit.ContainsKey(c)) continue;
                if (c is IDamageable d) d.TakeDamage(source.Resolve(c));
                rehit[c] = rehitCooldown;
            }
            UnityEngine.Pool.ListPool<ITargetable>.Release(cands);
        }

        private void DecayRehit(float dt)
        {
            if (rehit.Count == 0) return;
            List<ITargetable> keys = UnityEngine.Pool.ListPool<ITargetable>.Get();
            keys.AddRange(rehit.Keys);   // 스냅샷 후 수정(열거 중 수정 방지)
            for (int i = 0; i < keys.Count; i++)
            {
                ITargetable k = keys[i];
                float left = rehit[k] - dt;
                if (left <= 0f) rehit.Remove(k);
                else rehit[k] = left;
            }
            UnityEngine.Pool.ListPool<ITargetable>.Release(keys);
        }
    }
}
