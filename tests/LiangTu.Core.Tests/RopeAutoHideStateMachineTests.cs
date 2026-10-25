using LiangTu.Core;

namespace LiangTu.Core.Tests;

[TestClass]
public sealed class RopeAutoHideStateMachineTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Show_StartsTwoMinuteTimer()
    {
        var state = new RopeAutoHideStateMachine();

        state.Show(Start);

        Assert.IsTrue(state.IsVisible);
        Assert.IsFalse(state.Tick(Start + TimeSpan.FromSeconds(119)));
        Assert.IsTrue(state.Tick(Start + TimeSpan.FromSeconds(120)));
        Assert.IsFalse(state.IsVisible);
    }

    [TestMethod]
    public void Interaction_PausesAndRestartsFullTimer()
    {
        var state = new RopeAutoHideStateMachine();
        state.Show(Start);
        state.BeginInteraction(Start + TimeSpan.FromSeconds(45));

        Assert.IsFalse(state.Tick(Start + TimeSpan.FromMinutes(10)));

        state.EndInteraction(Start + TimeSpan.FromMinutes(10));
        Assert.IsFalse(state.Tick(Start + TimeSpan.FromMinutes(11) + TimeSpan.FromSeconds(59)));
        Assert.IsTrue(state.Tick(Start + TimeSpan.FromMinutes(12)));
    }

    [TestMethod]
    public void NestedInteractions_DoNotResumeUntilAllEnd()
    {
        var state = new RopeAutoHideStateMachine(autoHideDelay: TimeSpan.FromSeconds(10));
        state.Show(Start);
        state.BeginInteraction(Start + TimeSpan.FromSeconds(3));
        state.BeginInteraction(Start + TimeSpan.FromSeconds(4));
        state.EndInteraction(Start + TimeSpan.FromSeconds(5));

        Assert.IsTrue(state.IsInteractionActive);
        Assert.IsFalse(state.Tick(Start + TimeSpan.FromMinutes(5)));

        state.EndInteraction(Start + TimeSpan.FromMinutes(5));
        Assert.IsFalse(state.Tick(Start + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(9)));
        Assert.IsTrue(state.Tick(Start + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public void ManualHide_StopsTimerAndNextShowRestartsIt()
    {
        var state = new RopeAutoHideStateMachine();
        state.Show(Start);
        state.HideImmediately();

        Assert.IsFalse(state.Tick(Start + TimeSpan.FromMinutes(5)));

        state.Show(Start + TimeSpan.FromMinutes(5));
        Assert.IsTrue(state.IsVisible);
        Assert.IsFalse(state.Tick(Start + TimeSpan.FromMinutes(6) - TimeSpan.FromSeconds(1)));
        Assert.IsTrue(state.Tick(Start + TimeSpan.FromMinutes(7)));
    }

    [TestMethod]
    public void DisabledAutoHide_KeepsRopeVisible()
    {
        var state = new RopeAutoHideStateMachine(autoHideEnabled: false);
        state.Show(Start);

        Assert.IsFalse(state.Tick(Start + TimeSpan.FromHours(4)));
        Assert.IsTrue(state.IsVisible);
    }
}
