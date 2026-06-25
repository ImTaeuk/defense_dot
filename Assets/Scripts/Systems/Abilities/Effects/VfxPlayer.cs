// 연출 통합 래퍼 — 파티클/애니메이터/VFX Graph 를 단일 Play·Stop·길이산출로 통일
using UnityEngine;
using UnityEngine.VFX;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary>
    /// 파티클/Animator/VFX Graph 연출을 하나의 인터페이스로 재생·정지하고,
    /// 실제 길이를 산출해 일회성 자동 종료를 통일하는 래퍼입니다.
    /// </summary>
    public sealed class VfxPlayer : MonoBehaviour
    {
        [SerializeField] private bool loop;                  // 지속형(외부 정지) vs 일회성(자동 종료)
        [SerializeField] private float fallbackDuration = 2f;

        private ParticleSystem[] particles;
        private Animator[] animators;
        private VisualEffect[] vfxGraphs;
        private bool cached;

        /// <summary> 지속형 여부입니다. </summary>
        public bool Loop => loop;

        private void Cache()
        {
            if (cached) return;
            particles = GetComponentsInChildren<ParticleSystem>(true);
            animators = GetComponentsInChildren<Animator>(true);
            vfxGraphs = GetComponentsInChildren<VisualEffect>(true);
            cached = true;
        }

        /// <summary> 보유한 모든 연출을 재생합니다. </summary>
        public void Play()
        {
            Cache();
            for (int i = 0; i < particles.Length; i++) particles[i].Play(true);
            for (int i = 0; i < vfxGraphs.Length; i++) vfxGraphs[i].Play();
            // Animator: 활성 상태머신이 클립을 자동 재생하므로 명시 트리거 불필요.
        }

        /// <summary> 보유한 모든 연출을 정지합니다. </summary>
        public void Stop()
        {
            Cache();
            for (int i = 0; i < particles.Length; i++) particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            for (int i = 0; i < vfxGraphs.Length; i++) vfxGraphs[i].Stop();
        }

        /// <summary> 일회성 재생의 실제 길이(초)를 산출합니다. (파티클·클립·fallback 중 최대) </summary>
        public float ResolveDuration()
        {
            Cache();
            float d = fallbackDuration;
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem.MainModule m = particles[i].main;
                float t = m.duration + m.startLifetime.constantMax;
                if (t > d) d = t;
            }
            for (int i = 0; i < animators.Length; i++)
            {
                Animator a = animators[i];
                if (a == null || a.runtimeAnimatorController == null) continue;
                AnimationClip[] clips = a.runtimeAnimatorController.animationClips;
                for (int c = 0; c < clips.Length; c++) if (clips[c].length > d) d = clips[c].length;
            }
            return d;
        }

        /// <summary> 지속형 플래그를 설정합니다. </summary>
        public void SetLoop(bool value) { loop = value; }

        /// <summary> 일회성 연출 스폰 — 래퍼 보장·재생 후 실제 길이만큼 뒤 파괴합니다. </summary>
        public static VfxPlayer SpawnOneShot(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, pos, rot);
            VfxPlayer vp = go.GetComponent<VfxPlayer>();
            if (vp == null) vp = go.AddComponent<VfxPlayer>();
            vp.Play();
            Destroy(go, vp.ResolveDuration());
            return vp;
        }

        /// <summary> 지속 연출 인스턴스에 래퍼를 보장하고 재생합니다. (수명은 호출자가 관리) </summary>
        public static VfxPlayer EnsurePlay(GameObject instance, bool loop)
        {
            if (instance == null) return null;
            VfxPlayer vp = instance.GetComponent<VfxPlayer>();
            if (vp == null) vp = instance.AddComponent<VfxPlayer>();
            vp.SetLoop(loop);
            vp.Play();
            return vp;
        }
    }
}
