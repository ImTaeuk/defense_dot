// Sweeper 적 연출 — 이동방향 Y회전 + 피격 emission 플래시 + 사망 축소 (IDeathVisual)
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// Sweeper 3D 적의 비주얼 연출입니다. 이동 방향으로 회전하고, 피격 시 emission 플래시,
    /// 사망 시 애니메이션을 멈추고 축소 연출 후 완료를 통지합니다. (IDeathVisual)
    /// </summary>
    public sealed class SweeperEnemyVisual : MonoBehaviour, IDeathVisual
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SkinnedMeshRenderer[] renderers;
        [SerializeField] private Material[] colorMaterials;   // Mint/Pink/Yellow (URP/Lit)
        [SerializeField] private float rotateSpeed = 10f;
        [SerializeField] private float deathDuration = 0.6f;
        [SerializeField] private float hitFlashDuration = 0.12f;
        [SerializeField] private float hitFlashIntensity = 5f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private MonsterActor owner;
        private Material runtimeMat;     // 적별 머티리얼 인스턴스
        private Vector3 initialScale;
        private float hitTimer;
        private bool locked;            // 사망 중 회전·피격 잠금
        private Vector3 lastPosition;

        private void Awake()
        {
            initialScale = transform.localScale;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private void OnEnable()
        {
            if (owner == null) owner = GetComponentInParent<MonsterActor>();
            if (owner != null)
            {
                owner.OnHit -= HandleHit;
                owner.OnHit += HandleHit;
            }
            ResetVisual();
        }

        private void OnDisable()
        {
            if (owner != null) owner.OnHit -= HandleHit;
        }

        private void OnDestroy()
        {
            if (runtimeMat != null) Destroy(runtimeMat);
        }

        /// <summary> 풀 재사용 시 연출 상태를 초기화하고 색을 재랜덤합니다. </summary>
        private void ResetVisual()
        {
            locked = false;
            hitTimer = 0f;
            lastPosition = transform.position;
            transform.localScale = initialScale;   // 스케일 복원
            if (animator != null) animator.speed = 1f;
            ApplyRandomColor();
            SetEmission(0f);
        }

        private void ApplyRandomColor()
        {
            if (colorMaterials == null || colorMaterials.Length == 0 || renderers == null) return;
            Material picked = colorMaterials[Random.Range(0, colorMaterials.Length)];
            if (runtimeMat == null) runtimeMat = new Material(picked);
            else runtimeMat.CopyPropertiesFromMaterial(picked);
            runtimeMat.EnableKeyword("_EMISSION");   // 피격 플래시용
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterial = runtimeMat;
        }

        private void LateUpdate()
        {
            if (locked) { lastPosition = transform.position; return; }
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 1e-7f) return;
            Quaternion want = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, rotateSpeed * Time.deltaTime);
        }

        private void Update()
        {
            if (hitTimer <= 0f) return;
            hitTimer -= Time.deltaTime;
            float t = hitTimer > 0f ? Mathf.Clamp01(hitTimer / hitFlashDuration) : 0f;
            SetEmission(t);
        }

        private void HandleHit()
        {
            if (!locked) hitTimer = hitFlashDuration;
        }

        #region IDeathVisual
        /// <summary> 애니메이션을 멈추고 축소 연출을 재생한 뒤 onComplete를 호출합니다. </summary>
        public void PlayDeath(System.Action onComplete)
        {
            locked = true;
            if (animator != null) animator.speed = 0f;
            SetEmission(0f);
            DeathRoutine(onComplete).Forget();
        }
        #endregion

        private async UniTaskVoid DeathRoutine(System.Action onComplete)
        {
            System.Threading.CancellationToken token = this.GetCancellationTokenOnDestroy();
            float elapsed = 0f;
            while (elapsed < deathDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / deathDuration);
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, k);
                await UniTask.Yield(token);
            }
            transform.localScale = Vector3.zero;
            onComplete?.Invoke();
        }

        private void SetEmission(float t)
        {
            // 피격 플래시 emission
            if (runtimeMat != null) runtimeMat.SetColor(EmissionId, Color.white * (t * hitFlashIntensity));
        }
    }
}
