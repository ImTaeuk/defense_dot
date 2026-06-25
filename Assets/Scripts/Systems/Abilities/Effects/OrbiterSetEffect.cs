// 회전 위성 효과 — count개 위성이 회전, 반경은 최근접 적 추종, 접촉 적에 재타격 쿨다운 데미지
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 코어 주위를 도는 위성 집합 효과입니다. (상시) </summary>
    public sealed class OrbiterSetEffect : AbilityEffect
    {
        [SerializeField] private GameObject orbVisualPrefab;   // 위성 1개 비주얼(없으면 구체)
        [SerializeField] private float hitRadius = 0.6f;
        [SerializeField] private float rehitCooldown = 0.3f;
        [SerializeField] private float minRadius = 1.5f;
        [SerializeField] private float maxRadius = 12f;

        private TargetFinder finder;
        private float damage;
        private float rotSpeed;
        private Vector3 center;
        private float angle;
        private float radius = 3f;
        private float targetRadius = 3f;
        private readonly List<Transform> orbs = new List<Transform>();
        private readonly Dictionary<ITargetable, float> rehit = new Dictionary<ITargetable, float>();

        /// <summary> 위성 집합을 활성화합니다. </summary>
        public void Activate(Vector3 center, int count, float damage, float rotSpeed, TargetFinder finder)
        {
            this.center = center;
            this.damage = damage;
            this.rotSpeed = rotSpeed;
            this.finder = finder;
            transform.position = center;
            EnsureOrbs(Mathf.Max(1, count));
        }

        private void EnsureOrbs(int count)
        {
            for (int i = orbs.Count; i < count; i++)
            {
                GameObject o;
                if (orbVisualPrefab != null)
                {
                    o = Instantiate(orbVisualPrefab, transform);   // 프리팹 자체 스케일 유지
                }
                else
                {
                    o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Collider col = o.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    o.transform.localScale = Vector3.one * 0.4f;   // 폴백 구체만 축소
                }
                o.transform.SetParent(transform, false);
                VfxPlayer.EnsurePlay(o, true);
                orbs.Add(o.transform);
            }
            for (int i = 0; i < orbs.Count; i++) orbs[i].gameObject.SetActive(i < count);
        }

        public override void OnDespawn() { rehit.Clear(); }

        private void Update()
        {
            float dt = Time.deltaTime;
            angle += rotSpeed * dt;

            ITargetable t = finder != null ? finder.FindNearest(center, maxRadius + 5f) : null;
            if (t != null) targetRadius = Vector3.Distance(center, t.Position);
            radius = Mathf.Lerp(radius, Mathf.Clamp(targetRadius, minRadius, maxRadius), dt * 3f);

            DecayRehit(dt);

            int active = 0;
            for (int i = 0; i < orbs.Count; i++) if (orbs[i].gameObject.activeSelf) active++;
            int idx = 0;
            for (int i = 0; i < orbs.Count; i++)
            {
                if (!orbs[i].gameObject.activeSelf) continue;
                float a = angle + (Mathf.PI * 2f / Mathf.Max(1, active)) * idx;
                Vector3 pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                orbs[i].position = pos;
                DamageAround(pos);
                idx++;
            }
        }

        private void DamageAround(Vector3 pos)
        {
            if (finder == null) return;
            List<ITargetable> cands = UnityEngine.Pool.ListPool<ITargetable>.Get();
            finder.FindAllInRange(pos, hitRadius, cands);
            for (int i = 0; i < cands.Count; i++)
            {
                ITargetable c = cands[i];
                if (rehit.ContainsKey(c)) continue;
                if (c is IDamageable d) d.TakeDamage(damage);
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
