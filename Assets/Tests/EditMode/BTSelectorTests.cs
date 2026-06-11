using NUnit.Framework;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class BTSelectorTests
    {
        private sealed class StubNode : BTNode
        {
            private readonly NodeStatus status;
            public int EvalCount { get; private set; }
            public StubNode(NodeStatus status) { this.status = status; }
            public override NodeStatus Evaluate(Blackboard blackboard) { EvalCount++; return status; }
        }

        [Test]
        public void AllFailure_ReturnsFailure()
        {
            var sel = new Selector(new BTNode[] { new StubNode(NodeStatus.Failure), new StubNode(NodeStatus.Failure) });
            Assert.AreEqual(NodeStatus.Failure, sel.Evaluate(new Blackboard()));
        }

        [Test]
        public void FirstSuccess_ReturnsSuccess_AndSkipsRest()
        {
            var second = new StubNode(NodeStatus.Failure);
            var sel = new Selector(new BTNode[] { new StubNode(NodeStatus.Success), second });
            Assert.AreEqual(NodeStatus.Success, sel.Evaluate(new Blackboard()));
            Assert.AreEqual(0, second.EvalCount, "Success 이후 자식은 평가되지 않아야 함");
        }

        [Test]
        public void RunningChild_ReturnsRunning_AndSkipsRest()
        {
            var second = new StubNode(NodeStatus.Failure);
            var sel = new Selector(new BTNode[] { new StubNode(NodeStatus.Running), second });
            Assert.AreEqual(NodeStatus.Running, sel.Evaluate(new Blackboard()));
            Assert.AreEqual(0, second.EvalCount);
        }
    }
}
