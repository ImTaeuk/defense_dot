using NUnit.Framework;
using DefenseDot.Systems.Actor;

namespace DefenseDot.Tests.EditMode
{
    public class BTLeafBuilderTests
    {
        [Test]
        public void Condition_True_Success_False_Failure()
        {
            var t = new ConditionLeaf(bb => true);
            var f = new ConditionLeaf(bb => false);
            Assert.AreEqual(NodeStatus.Success, t.Evaluate(new Blackboard()));
            Assert.AreEqual(NodeStatus.Failure, f.Evaluate(new Blackboard()));
        }

        [Test]
        public void Action_ReturnsProvidedStatus()
        {
            var a = new ActionLeaf(bb => NodeStatus.Running);
            Assert.AreEqual(NodeStatus.Running, a.Evaluate(new Blackboard()));
        }

        [Test]
        public void Leaf_CanReadAndWriteBlackboard()
        {
            var bb = new Blackboard { stunTimer = 1.5f };
            var cond = new ConditionLeaf(b => b.stunTimer > 0f);
            var act = new ActionLeaf(b => { b.stunTimer = 0f; return NodeStatus.Success; });
            Assert.AreEqual(NodeStatus.Success, cond.Evaluate(bb));
            act.Evaluate(bb);
            Assert.AreEqual(0f, bb.stunTimer);
        }

        [Test]
        public void Builder_ComposesEquivalentTree()
        {
            // Selector[ Sequence[ Condition(false), Action(Success) ], Action(Success) ]
            // 첫 Sequence 는 Condition(false)로 Failure → Selector 가 둘째 Action(Success) 채택
            var tree = BT.Selector(
                BT.Sequence(
                    BT.Condition(bb => false),
                    BT.Action(bb => NodeStatus.Success)),
                BT.Action(bb => NodeStatus.Success));
            Assert.AreEqual(NodeStatus.Success, tree.Evaluate(new Blackboard()));
        }
    }
}
