using NUnit.Framework;
using UnityEngine;
using DefenseDot.UI.Hover;

namespace DefenseDot.Tests.EditMode
{
    public sealed class HoverMediatorTests
    {
        /// <summary> 마지막으로 진입 통보된 내용. </summary>
        private HoverContent lastEntered;

        private int enteredCount;
        private int exitedCount;

        /// <summary> 매 테스트 전 중재자와 집계를 비웁니다. </summary>
        [SetUp]
        public void SetUp()
        {
            HoverMediator.Reset();
            lastEntered = default;
            enteredCount = 0;
            exitedCount = 0;
            HoverMediator.OnHoverEntered += HandleEntered;
            HoverMediator.OnHoverExited += HandleExited;
        }

        /// <summary> 매 테스트 후 중재자 상태가 다음 테스트로 새지 않게 비웁니다. </summary>
        [TearDown]
        public void TearDown()
        {
            HoverMediator.Reset();
        }

        /// <summary> 진입 통보를 집계합니다. </summary>
        /// <param name="content">전달된 표시 내용</param>
        private void HandleEntered(HoverContent content)
        {
            lastEntered = content;
            enteredCount++;
        }

        /// <summary> 이탈 통보를 집계합니다. </summary>
        private void HandleExited()
        {
            exitedCount++;
        }

        /// <summary> 진입 통보 시 요소의 텍스트·위치가 그대로 발화되는지 검사합니다. </summary>
        [Test]
        public void NotifyEnteredRaisesContentOfHoverable()
        {
            StubHoverable a = new StubHoverable("A", 1f);

            HoverMediator.NotifyEntered(a);

            Assert.AreEqual(1, enteredCount);
            Assert.AreEqual("A", lastEntered.Text);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), lastEntered.Position);
        }

        /// <summary> 단독 요소가 이탈하면 이탈 이벤트가 1회 발화되는지 검사합니다. </summary>
        [Test]
        public void NotifyExitedRaisesExitWhenQueueBecomesEmpty()
        {
            StubHoverable a = new StubHoverable("A", 1f);
            HoverMediator.NotifyEntered(a);

            HoverMediator.NotifyExited(a);

            Assert.AreEqual(1, exitedCount);
        }

        /// <summary> 나중에 진입한 안쪽 요소로 표시가 교체되는지 검사합니다. </summary>
        [Test]
        public void InnerEnterReplacesOuterDisplay()
        {
            StubHoverable outer = new StubHoverable("outer", 1f);
            StubHoverable inner = new StubHoverable("inner", 2f);
            HoverMediator.NotifyEntered(outer);

            HoverMediator.NotifyEntered(inner);

            Assert.AreEqual(2, enteredCount);
            Assert.AreEqual("inner", lastEntered.Text);
        }

        /// <summary> 안쪽이 이탈하면 바깥쪽 내용으로 복귀하는지 검사합니다. </summary>
        [Test]
        public void InnerExitFallsBackToOuterContent()
        {
            StubHoverable outer = new StubHoverable("outer", 1f);
            StubHoverable inner = new StubHoverable("inner", 2f);
            HoverMediator.NotifyEntered(outer);
            HoverMediator.NotifyEntered(inner);

            HoverMediator.NotifyExited(inner);

            Assert.AreEqual(0, exitedCount);
            Assert.AreEqual(3, enteredCount);
            Assert.AreEqual("outer", lastEntered.Text);
        }

        /// <summary> 바깥쪽만 이탈하면 표시 대상이 그대로 유지되는지 검사합니다. </summary>
        [Test]
        public void OuterExitKeepsInnerDisplayUntouched()
        {
            StubHoverable outer = new StubHoverable("outer", 1f);
            StubHoverable inner = new StubHoverable("inner", 2f);
            HoverMediator.NotifyEntered(outer);
            HoverMediator.NotifyEntered(inner);
            int enteredBefore = enteredCount;

            HoverMediator.NotifyExited(outer);

            Assert.AreEqual(enteredBefore, enteredCount);
            Assert.AreEqual(0, exitedCount);
            Assert.AreEqual("inner", lastEntered.Text);
        }

        /// <summary> 중첩 요소가 모두 이탈하면 마지막에 이탈이 1회 발화되는지 검사합니다. </summary>
        [Test]
        public void ExitingAllHoverablesRaisesExitOnce()
        {
            StubHoverable outer = new StubHoverable("outer", 1f);
            StubHoverable inner = new StubHoverable("inner", 2f);
            HoverMediator.NotifyEntered(outer);
            HoverMediator.NotifyEntered(inner);

            HoverMediator.NotifyExited(inner);
            HoverMediator.NotifyExited(outer);

            Assert.AreEqual(1, exitedCount);
        }

        /// <summary> 큐에 없는 요소의 이탈 통보가 무시되는지 검사합니다. </summary>
        [Test]
        public void ExitOfUnknownHoverableIsIgnored()
        {
            StubHoverable known = new StubHoverable("known", 1f);
            StubHoverable stranger = new StubHoverable("stranger", 2f);
            HoverMediator.NotifyEntered(known);
            int enteredBefore = enteredCount;

            HoverMediator.NotifyExited(stranger);

            Assert.AreEqual(enteredBefore, enteredCount);
            Assert.AreEqual(0, exitedCount);
            Assert.AreEqual("known", lastEntered.Text);
        }

        /// <summary> 같은 요소가 두 번 진입해도 이탈 한 번에 큐가 비는지 검사합니다. </summary>
        [Test]
        public void DuplicateEnterDoesNotStackInQueue()
        {
            StubHoverable a = new StubHoverable("A", 1f);
            HoverMediator.NotifyEntered(a);
            HoverMediator.NotifyEntered(a);

            HoverMediator.NotifyExited(a);

            Assert.AreEqual(1, exitedCount);
        }

        /// <summary> null 통보가 예외 없이 무시되는지 검사합니다. </summary>
        [Test]
        public void NullNotificationsAreIgnored()
        {
            HoverMediator.NotifyEntered(null);
            HoverMediator.NotifyExited(null);

            Assert.AreEqual(0, enteredCount);
            Assert.AreEqual(0, exitedCount);
        }

        /// <summary> Reset 이 구독과 큐를 모두 비우는지 검사합니다. </summary>
        [Test]
        public void ResetClearsSubscribersAndQueue()
        {
            StubHoverable a = new StubHoverable("A", 1f);
            HoverMediator.NotifyEntered(a);

            HoverMediator.Reset();
            HoverMediator.NotifyEntered(a);
            HoverMediator.NotifyExited(a);

            Assert.AreEqual(1, enteredCount);
            Assert.AreEqual(0, exitedCount);
        }

        /// <summary> 구독자가 없어도 통보가 예외 없이 처리되는지 검사합니다. </summary>
        [Test]
        public void NotificationsWithoutSubscribersDoNotThrow()
        {
            HoverMediator.Reset();
            StubHoverable a = new StubHoverable("A", 1f);

            Assert.DoesNotThrow(() => HoverMediator.NotifyEntered(a));
            Assert.DoesNotThrow(() => HoverMediator.NotifyExited(a));
        }

        /// <summary> MonoBehaviour 가 아닌 순수 C# 구현체도 표시 대상이 되는지 검사합니다. </summary>
        [Test]
        public void PlainCSharpHoverableIsAccepted()
        {
            StubHoverable plain = new StubHoverable("plain", 5f);

            HoverMediator.NotifyEntered(plain);

            Assert.AreEqual(1, enteredCount);
            Assert.AreEqual("plain", lastEntered.Text);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), lastEntered.Position);
        }

        /// <summary> 고정된 표시 내용을 돌려주는 테스트용 호버 대상입니다. </summary>
        private sealed class StubHoverable : IUIHoverable
        {
            /// <summary> 이 스텁이 항상 돌려줄 표시 내용. </summary>
            private readonly HoverContent content;

            /// <summary> 표시할 문구와 위치 X 성분을 받아 스텁을 만듭니다. </summary>
            /// <param name="text">표시할 문구</param>
            /// <param name="x">위치의 X 성분</param>
            public StubHoverable(string text, float x)
            {
                content = new HoverContent(text, new Vector3(x, 0f, 0f));
            }

            /// <summary> 준비된 표시 내용을 그대로 돌려줍니다. </summary>
            public HoverContent BuildHoverContent()
            {
                return content;
            }
        }
    }
}