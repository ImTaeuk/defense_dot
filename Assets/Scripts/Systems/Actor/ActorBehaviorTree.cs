using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Actor
{
    /// <summary>
    /// 액터의 행동을 BT로 구동하는 추상 러너입니다. (액터와 같은 GameObject에 부착)
    /// 매 tick 트리를 평가하며, ActorState의 유일한 writer입니다.
    /// </summary>
    public abstract class ActorBehaviorTree : MonoBehaviour
    {
        protected IActor actor;
        private readonly Blackboard blackboard = new Blackboard();
        private BTNode root;

        /// <summary> 노드 간 공유 데이터(외부 CC가 stunTimer 기록, 테스트 주입). </summary>
        public Blackboard Blackboard => blackboard;

        /// <summary> 구동 대상 액터. </summary>
        public IActor Actor => actor;

        /// <summary> 액터 참조를 캐싱합니다. </summary>
        protected virtual void Awake() { actor = GetComponent<IActor>(); }

        /// <summary> 스폰(재활성) 시 blackboard를 초기화하고 트리를 1회 빌드합니다. </summary>
        protected virtual void OnEnable()
        {
            blackboard.target = null;
            blackboard.stunTimer = 0f;
            if (root == null) root = BuildTree();
        }

        private void Update() { Tick(); }

        /// <summary> 트리를 1회 평가합니다. (Dead면 정지) </summary>
        public void Tick()
        {
            if (actor == null) actor = GetComponent<IActor>();   // 생명주기 미보장 환경 대비
            if (actor == null || actor.CurrentState == ActorState.Dead) return;
            if (root == null) root = BuildTree();
            root.Evaluate(blackboard);
        }

        /// <summary> 공통 골격: stun 처리(우선) → 액터별 primary. </summary>
        protected virtual BTNode BuildTree()
        {
            return BT.Selector(
                BT.Sequence(
                    new IsStunnedCondition(),
                    new TickStunAction(actor)),
                BuildPrimary());
        }

        /// <summary> 액터별 주 행동을 조립합니다. (오버라이드 지점) </summary>
        protected abstract BTNode BuildPrimary();
    }
}
