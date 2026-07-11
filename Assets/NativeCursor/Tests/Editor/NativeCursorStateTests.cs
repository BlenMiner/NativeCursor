using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Riten.Native.Cursors.Tests
{
    public class NativeCursorStateTests
    {
        private sealed class RecordingCursorService : ICursorService, ICursorServiceLifecycle
        {
            public readonly List<string> Calls = new();

            public bool SetCursor(NTCursors ntCursor)
            {
                Calls.Add($"Set:{ntCursor}");
                return true;
            }

            public void ResetCursor()
            {
                Calls.Add("Reset");
            }

            public void OnActivated()
            {
                Calls.Add("Activate");
            }

            public void OnDeactivated()
            {
                Calls.Add("Deactivate");
            }
        }

        [SetUp]
        public void SetUp()
        {
            ResetRuntimeState();
        }

        [TearDown]
        public void TearDown()
        {
            NativeCursor.SetService(null);
            ResetRuntimeState();
        }

        [Test]
        public void SetService_DeactivatesPreviousServiceBeforeActivatingReplacement()
        {
            var first = new RecordingCursorService();
            var second = new RecordingCursorService();

            NativeCursor.SetService(first);
            NativeCursor.SetCursor(NTCursors.Link);
            NativeCursor.SetService(second);

            CollectionAssert.AreEqual(
                new[] { "Activate", "Set:Default", "Set:Link", "Reset", "Deactivate" },
                first.Calls);
            CollectionAssert.AreEqual(
                new[] { "Activate", "Set:Link" },
                second.Calls);

            NativeCursor.SetService(null);
            CollectionAssert.AreEqual(
                new[] { "Activate", "Set:Link", "Reset", "Deactivate" },
                second.Calls);
        }

        [Test]
        public void SubsystemReset_DoesNotCarryRequestedCursorIntoNextSession()
        {
            var firstSession = new RecordingCursorService();
            NativeCursor.SetService(firstSession);
            NativeCursor.SetCursor(NTCursors.ClosedHand);

            InvokeStaticReset(typeof(NativeCursor));

            var secondSession = new RecordingCursorService();
            NativeCursor.SetService(secondSession);

            CollectionAssert.AreEqual(new[] { "Activate", "Set:Default" }, secondSession.Calls);
        }

        [Test]
        public void InactiveStackMutation_ReappliesActiveCursor()
        {
            var service = new RecordingCursorService();
            NativeCursor.SetService(service);

            var activeId = CursorStack.Push(NTCursors.Link, priority: 10);
            var inactiveId = CursorStack.Push(NTCursors.Crosshair, priority: 0);

            Assert.That(CursorStack.Peek().id, Is.EqualTo(activeId));
            Assert.That(service.Calls[^1], Is.EqualTo("Set:Link"));

            Assert.That(CursorStack.Pop(inactiveId), Is.True);
            Assert.That(service.Calls[^1], Is.EqualTo("Set:Link"));
        }

        [Test]
        public void UnpausingStack_AppliesLatestWinningCursor()
        {
            var service = new RecordingCursorService();
            NativeCursor.SetService(service);
            var callsBeforePause = service.Calls.Count;

            CursorStack.PauseRendering(true);
            CursorStack.Push(NTCursors.IBeam, priority: 2);
            CursorStack.Push(NTCursors.Invalid, priority: 3);

            Assert.That(service.Calls.Count, Is.EqualTo(callsBeforePause));

            CursorStack.PauseRendering(false);
            Assert.That(service.Calls[^1], Is.EqualTo("Set:Invalid"));
        }

        [Test]
        public void EqualPriority_UsesSecondaryPriorityThenMostRecentEntry()
        {
            var first = CursorStack.Push(NTCursors.Link, priority: 2, secondaryPriority: 5);
            var second = CursorStack.Push(NTCursors.IBeam, priority: 2, secondaryPriority: 5);
            Assert.That(CursorStack.Peek().id, Is.EqualTo(second));

            CursorStack.Update(first, NTCursors.Crosshair, secondaryPriority: 6);
            Assert.That(CursorStack.Peek().id, Is.EqualTo(first));
        }

        private static void ResetRuntimeState()
        {
            NativeCursor.SetService(null);
            InvokeStaticReset(typeof(NativeCursor));
            InvokeStaticReset(typeof(CursorStack));
        }

        private static void InvokeStaticReset(System.Type type)
        {
            var method = type.GetMethod("ResetStaticState", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{type.Name} must expose its SubsystemRegistration reset method.");
            method.Invoke(null, null);
        }
    }
}
