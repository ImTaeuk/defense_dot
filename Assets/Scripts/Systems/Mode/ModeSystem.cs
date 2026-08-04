// 모드별 씬 시스템 베이스 — 모드(IGameMode)를 만들고 한 프레임씩 돌린다
using UnityEngine;
using DefenseDot.Domain;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 씬이 어떤 모드인지 정하고 그 모드를 만들어 돌리는 베이스입니다.
    /// 연출 배선은 ModeVisualBinder 에 맡기고, 무엇을 비출지(중심)만 정해 넘깁니다.
    /// (인터페이스 대신 추상 MonoBehaviour — 인스펙터 직렬화)
    /// </summary>
    public abstract class ModeSystem : MonoBehaviour
    {
        /// <summary> 이 씬의 연출 배선. 없으면 연출 없이 진행한다. </summary>
        [SerializeField] private ModeVisualBinder visual;

        /// <summary> 공통 입력을 받아 이 씬의 모드를 생성합니다. </summary>
        public abstract IGameMode CreateMode(ModeContext ctx);

        /// <summary> 이 모드의 적 수 표시 한계(HUD capacity)입니다. </summary>
        public abstract int EnemyDisplayCapacity { get; }

        /// <summary> 이 모드의 코어 최대 HP입니다. (Grid=본진 HP, Arena=수용 한계) </summary>
        public abstract float CoreMaxHp { get; }

        /// <summary> 이 모드만 쓰는 자원을 컨텍스트에 채웁니다. 공통 자원은 합성 루트가 이미 채웠습니다. </summary>
        /// <param name="builder">조립 중인 UI 컨텍스트. 모드 전용 칸만 채운다</param>
        public virtual void FillContext(GameContextBuilder builder)
        {
        }

        /// <summary> 이 모드가 소유한 시스템을 한 프레임 진행시킵니다. </summary>
        /// <param name="deltaTime">경과 시간</param>
        public virtual void Tick(float deltaTime)
        {
        }

        /// <summary> 연출 배선을 요청합니다. 중심만 이쪽이 정하고 나머지는 바인더가 맡습니다. </summary>
        /// <param name="ctx">코어 중심 등을 담은 모드 컨텍스트</param>
        protected void BindVisual(in ModeContext ctx)
        {
            if (visual == null)
                return;

            visual.Bind(GetCameraCenter(ctx));
        }

        /// <summary> 카메라가 바라볼 중심을 반환합니다. 기본은 코어 중심(모드별 재정의 가능). </summary>
        protected virtual Vector3 GetCameraCenter(in ModeContext ctx)
        {
            return ctx.CoreCenter;
        }
    }
}
