using NUnit.Framework;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class BTNodeTests
    {
        /// <summary> 지정한 결과를 그대로 돌려주는 테스트용 노드. </summary>
        private sealed class FixedResultNode : BTNode
        {
            private readonly NodeStatus result;

            public FixedResultNode(NodeStatus result)
            {
                this.result = result;
            }

            public override NodeStatus Evaluate(Blackboard blackboard)
            {
                return result;
            }
        }

        /// <summary> 평가 시 기절 타이머를 지우는 테스트용 노드. </summary>
        private sealed class ClearStunAction : ActionNode
        {
            public override NodeStatus Evaluate(Blackboard blackboard)
            {
                blackboard.stunTimer = 0f;
                return NodeStatus.Success;
            }
        }

        [Test]
        public void IsStunnedCondition_StunActive_ReturnsSuccess()
        {
            var node = new IsStunnedCondition();
            Assert.AreEqual(NodeStatus.Success, node.Evaluate(new Blackboard { stunTimer = 1.5f }));
        }

        [Test]
        public void IsStunnedCondition_NoStun_ReturnsFailure()
        {
            var node = new IsStunnedCondition();
            Assert.AreEqual(NodeStatus.Failure, node.Evaluate(new Blackboard()));
        }

        [Test]
        public void Node_CanReadAndWriteBlackboard()
        {
            var bb = new Blackboard { stunTimer = 1.5f };
            var condition = new IsStunnedCondition();
            var clear = new ClearStunAction();
            Assert.AreEqual(NodeStatus.Success, condition.Evaluate(bb));
            clear.Evaluate(bb);
            Assert.AreEqual(0f, bb.stunTimer);
            Assert.AreEqual(NodeStatus.Failure, condition.Evaluate(bb), "타이머가 지워지면 조건도 뒤집힌다");
        }

        [Test]
        public void Selector_FirstBranchFails_TakesSecond()
        {
            // Selector[ Sequence[ Failure, Success ], Success ]
            // 첫 Sequence 는 Failure 로 중단 → Selector 가 둘째 가지를 채택
            var tree = BT.Selector(
                BT.Sequence(
                    new FixedResultNode(NodeStatus.Failure),
                    new FixedResultNode(NodeStatus.Success)),
                new FixedResultNode(NodeStatus.Success));
            Assert.AreEqual(NodeStatus.Success, tree.Evaluate(new Blackboard()));
        }
    }
}