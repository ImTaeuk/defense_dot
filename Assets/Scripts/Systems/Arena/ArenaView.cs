// 아레나 데이터를 Scene에 시각화 — 편집:기즈모 가이드 / 런타임:경계 갱신 구독
using UnityEngine;
using DefenseDot.Data;
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Arena
{
    /// <summary>
    /// 아레나 데이터를 Scene에 시각화하는 컴포넌트입니다.
    /// 편집 중에는 OnDrawGizmos로 동심원 가이드를, 런타임에는 ArenaModel 반경 변화를 구독합니다.
    /// </summary>
    public class ArenaView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ArenaConfig config;

        [Header("Gizmos")]
        [SerializeField] private bool showGizmos = true;

        /// <summary> 이 뷰가 단일 소유하는 아레나 설정입니다. (모드 부트스트랩이 참조) </summary>
        public ArenaConfig Config => config;

        private ArenaModel model;

        /// <summary> 런타임 모델을 바인딩하고 경계 갱신을 구독합니다. </summary>
        public void Bind(ArenaModel arenaModel)
        {
            model = arenaModel;
            model.OnRadiusChanged += HandleRadiusChanged;
            HandleRadiusChanged();
        }

        private void OnDestroy()
        {
            if (model != null) model.OnRadiusChanged -= HandleRadiusChanged;
        }

        private void HandleRadiusChanged()
        {
            // 경계 비주얼 갱신 지점
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos || config == null) return;

            Vector3 c = transform.position;
            float arenaR = model != null ? model.ArenaRadius : config.arenaRadius;
            float minR = model != null ? model.SpawnMinRadius : config.coreRadius + config.spawnInnerMargin;
            float maxR = model != null ? model.SpawnMaxRadius : config.arenaRadius - config.spawnOuterMargin;

            Gizmos.color = new Color(1f, 0.95f, 0.5f, 0.9f);   // 아레나 경계
            DrawCircle(c, arenaR);
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.9f);    // 코어
            DrawCircle(c, config.coreRadius);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);    // 스폰 밴드 안/밖
            DrawCircle(c, minR);
            DrawCircle(c, maxR);
        }

        private void DrawCircle(Vector3 center, float radius)
        {
            const int seg = 48;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
