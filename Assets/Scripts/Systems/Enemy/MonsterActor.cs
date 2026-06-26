// 적 액터 — 주입된 전략으로 이동, 처치/도달 분기, 풀링 대상
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 적(몬스터) 액터입니다. 주입된 이동 전략으로 이동하며, 처치/도달을 스포너에 통지합니다.
    /// </summary>
    public class MonsterActor : ActorBase<EnemyData>, IMovableActor, ITargetable, ICombatTargetInfo, IPoolable
    {
        private IMovementStrategy movement;
        private EnemySpawner spawner;
        private bool resolved;   // 처치/도달 이중 정산 방지
        private IDeathVisual deathVisual;   // 사망 연출(있으면 지연 반환)

        /// <summary> 피격(데미지 수신) 시 발생합니다. (3D 연출 구독용) </summary>
        public event System.Action OnHit;

        // 피격 플래시 (Visual 스프라이트만)
        private SpriteRenderer[] flashRenderers;
        private Color[] flashBaseColors;
        private float hitFlashTimer;
        private const float HitFlashDuration = 0.09f;

        /// <summary>
        /// 이 적의 데이터입니다. (풀 회수 시 prefab 식별용)
        /// </summary>
        public EnemyData Data => data;

        /// <summary>
        /// 처치 시 지급할 보상 골드입니다.
        /// </summary>
        public int RewardGold => data != null ? data.rewardGold : 0;

        /// <summary>
        /// 코어 도달 시 입히는 피해입니다. (EnemyData 공급, 미할당 시 1)
        /// </summary>
        public float CoreDamage => data != null ? data.coreDamage : 1f;

        /// <summary>
        /// 회수·통지를 위임할 스포너를 주입합니다.
        /// </summary>
        public void SetSpawner(EnemySpawner s) => spawner = s;

        /// <summary>
        /// 모드가 생성한 이동 전략을 주입합니다.
        /// </summary>
        public void SetMovement(IMovementStrategy strategy) => movement = strategy;

        /// <summary> 현재 주입된 이동 전략(브레인 리프가 live-read). </summary>
        public IMovementStrategy CurrentMovement => movement;

        #region IMovableActor Implementation
        public void SetPosition(Vector3 newPosition) => transform.position = newPosition;

        public bool IsMovableState()
        {
            return currentState == ActorState.Moving || currentState == ActorState.Idle;
        }
        #endregion

        #region ITargetable Implementation
        public bool IsActive => currentState != ActorState.Dead;
        #endregion

        #region ICombatTargetInfo Implementation
        /// <summary> 보스 여부. 보스 시스템 도입 전이라 항상 false. </summary>
        public bool IsBoss => false;
        /// <summary> 현재 체력 비율(0~1). </summary>
        public float HealthRatio => (data != null && data.health > 0f) ? Mathf.Clamp01(currentHealth / data.health) : 1f;
        #endregion

        #region IPoolable Implementation
        public void OnSpawn()
        {
            resolved = false;
            movement = null;
            if (data != null) currentHealth = data.health;
            SetState(ActorState.Idle);

            hitFlashTimer = 0f;
            if (flashRenderers != null)
                for (int i = 0; i < flashRenderers.Length; i++)
                    if (flashRenderers[i] != null) flashRenderers[i].color = flashBaseColors[i];
        }

        public void OnDespawn()
        {
            movement = null;
            SetState(ActorState.Dead);
        }
        #endregion

        public override void Initialize(EnemyData actorData)
        {
            base.Initialize(actorData);
            currentHealth = actorData.health;
            resolved = false;
            CacheFlashRenderers();
            if (deathVisual == null) deathVisual = GetComponentInChildren<IDeathVisual>(true);
        }

        private void CacheFlashRenderers()
        {
            if (flashRenderers != null) return;
            SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (!all[i].gameObject.name.Contains("Shadow")) n++;
            flashRenderers = new SpriteRenderer[n];
            flashBaseColors = new Color[n];
            int k = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.name.Contains("Shadow")) continue;
                flashRenderers[k] = all[i];
                flashBaseColors[k] = all[i].color;
                k++;
            }
        }

        private void Update()
        {
            if (hitFlashTimer <= 0f || flashRenderers == null) return;
            hitFlashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(hitFlashTimer / HitFlashDuration);
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] == null) continue;
                flashRenderers[i].color = Color.Lerp(flashBaseColors[i], Color.white, t);
            }
        }

        public override void TakeDamage(float amount)
        {
            if (currentState == ActorState.Dead || resolved) return;

            currentHealth -= amount;
            hitFlashTimer = HitFlashDuration;   // 2D 스프라이트 플래시
            OnHit?.Invoke();                     // 3D 연출 통지
            if (currentHealth <= 0f) Resolve(reached: false);
        }

        /// <summary> 경로 끝 도달 처리(브레인 리프가 호출). </summary>
        public void HandleReachedGoal() => Resolve(reached: true);

        /// <summary>
        /// 적의 최종 처리를 분기합니다. (도달=코어 피해, 처치=보상)
        /// </summary>
        private void Resolve(bool reached)
        {
            if (resolved) return;
            resolved = true;
            SetState(ActorState.Dead);

            if (reached) { spawner?.HandleEnemyReached(this); return; }
            if (deathVisual != null) deathVisual.PlayDeath(() => spawner?.HandleEnemyKilled(this));
            else spawner?.HandleEnemyKilled(this);
        }
    }
}
