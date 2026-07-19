// 회전 위성 효과 — count개 위성이 회전, 반경은 최근접 적 추종, 접촉 적에 재타격 쿨다운 데미지
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 코어 주위를 도는 위성 집합 효과입니다. (상시) </summary>
    public sealed class OrbiterSetEffect : AbilityEffect
    {
        [SerializeField] private GameObject orbVisualPrefab;   // 위성 1개 비주얼(없으면 구체)
        [SerializeField] private float hitRadius = 6f;
        [SerializeField] private float rehitCooldown = 0.3f;
        [SerializeField] private float minOrbitRadius = 6f;   // 적 없을 때 기본 궤도 반경(하한)
        [SerializeField] private float maxOrbitReach = 27f;   // 추종 반경 상한(적 거리 상한)
        [SerializeField] private float orbitHeight = 1f;      // 코어 기준 궤도 높이(캐릭터 몸 높이)
        [SerializeField] private float radiusFollowSpeed = 1.5f;   // 반경이 적 거리로 벌어지는 속도(클수록 빠름)

        private TargetFinder finder;
        private DamageSource source;
        private AssetReferenceGameObject hitVfx;   // 접촉 명중 VFX
        private float rotSpeed;
        private Vector3 center;
        private float angle;
        private float radius;   // 최근접 적 거리로 추종하는 현재 궤도 반경
        private readonly List<Transform> orbs = new List<Transform>();
        private readonly Dictionary<ITargetable, float> rehit = new Dictionary<ITargetable, float>();

        /// <summary> 위성 집합을 활성화합니다. </summary>
        public void Activate(Vector3 center, int count, DamageSource source, float rotSpeed, TargetFinder finder, AssetReferenceGameObject hitVfx)
        {
            this.center = center;
            this.source = source;
            this.rotSpeed = rotSpeed;
            this.finder = finder;
            this.hitVfx = hitVfx;
            this.radius = minOrbitRadius;
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

        /// <summary> 위성을 회전시키고 반경을 최근접 적 거리로 추종하며 접촉 적을 타격합니다. </summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            angle += rotSpeed * dt;

            DecayRehit(dt);

            // 최근접 적 거리로 반경을 부드럽게 추종(수평 XZ 기준, 적 없으면 하한)
            float targetRadius = minOrbitRadius;
            if (finder != null)
            {
                ITargetable t = finder.FindNearest(center, maxOrbitReach + 5f);
                if (t != null)
                {
                    Vector3 d = t.Position - center;
                    float xz = new Vector2(d.x, d.z).magnitude;
                    targetRadius = Mathf.Clamp(xz, minOrbitRadius, maxOrbitReach);
                }
            }
            radius = Mathf.Lerp(radius, targetRadius, dt * radiusFollowSpeed);

            // 활성 위성 수 집계 → 균등 위상 배치
            int active = 0;
            for (int i = 0; i < orbs.Count; i++)
            {
                if (orbs[i].gameObject.activeSelf)
                    active++;
            }

            // 코어 주위 원형 궤도에 배치하며 접촉 데미지
            int idx = 0;
            for (int i = 0; i < orbs.Count; i++)
            {
                if (!orbs[i].gameObject.activeSelf)
                    continue;
                float a = angle + (Mathf.PI * 2f / Mathf.Max(1, active)) * idx;
                Vector3 pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                pos.y = center.y + orbitHeight;
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
                if (rehit.ContainsKey(c))
                    continue;

                // 데미지 적용 + 접촉 지점에 명중 VFX 재생
                if (c is IDamageable d)
                {
                    d.TakeDamage(source.Resolve(c));
                    if (hitVfx != null && Spawner != null && hitVfx.RuntimeKeyIsValid())
                        Spawner.PlayOneShot(hitVfx, c.Position, Quaternion.identity);
                }
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
