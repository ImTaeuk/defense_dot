using NUnit.Framework;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class BTSequenceTests
    {
        /// <summary> 스크립트된 결과를 반환하고 평가 횟수를 세는 테스트용 노드. </summary>
        private sealed class StubNode : BTNode
        {
            private readonly NodeStatus status;
            public int EvalCount { get; private set; }
            public StubNode(NodeStatus status) { this.status = status; }
            public override NodeStatus Evaluate(Blackboard blackboard) { EvalCount++; return status; }
        }

        [Test]
        public void AllSuccess_ReturnsSuccess()
        {
            var seq = new Sequence(new BTNode[] { new StubNode(NodeStatus.Success), new StubNode(NodeStatus.Success) });
            Assert.AreEqual(NodeStatus.Success, seq.Evaluate(new Blackboard()));
        }

        [Test]
        public void FirstFailure_ReturnsFailure_AndSkipsRest()
        {
            var second = new StubNode(NodeStatus.Success);
            var seq = new Sequence(new BTNode[] { new StubNode(NodeStatus.Failure), second });
            Assert.AreEqual(NodeStatus.Failure, seq.Evaluate(new Blackboard()));
            Assert.AreEqual(0, second.EvalCount, "Failure 이후 자식은 평가되지 않아야 함");
        }

        [Test]
        public void RunningChild_ReturnsRunning_AndSkipsRest()
        {
            var second = new StubNode(NodeStatus.Success);
            var seq = new Sequence(new BTNode[] { new StubNode(NodeStatus.Running), second });
            Assert.AreEqual(NodeStatus.Running, seq.Evaluate(new Blackboard()));
            Assert.AreEqual(0, second.EvalCount);
        }
    }
}
