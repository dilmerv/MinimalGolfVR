using NUnit.Framework;
using MinimalGolf;

/// <summary>
/// Headless coverage for the hand-tracking input contract: unified first-input-wins
/// aim ownership across clubs/modalities (GolfAimLock) and press/release edge
/// evaluation shared by the trigger and pinch paths (GolfInputEdges).
/// </summary>
public sealed class HandAimLogicTests
{
    [Test]
    public void AimLock_FirstAcquirerWinsSecondIsBlocked()
    {
        var clubA = new object();
        var clubB = new object();
        try
        {
            Assert.IsTrue(GolfAimLock.TryAcquire(clubA), "first acquirer should win");
            Assert.IsTrue(GolfAimLock.IsHeldByOther(clubB), "second contender must see the lock held");
            Assert.IsFalse(GolfAimLock.TryAcquire(clubB), "second acquirer must be blocked");
            Assert.IsFalse(GolfAimLock.IsHeldByOther(clubA), "owner must not see its own lock as foreign");
        }
        finally
        {
            GolfAimLock.Release(clubA);
        }
    }

    [Test]
    public void AimLock_ReleaseFreesOwnershipForNextInput()
    {
        var clubA = new object();
        var clubB = new object();
        try
        {
            Assert.IsTrue(GolfAimLock.TryAcquire(clubA));
            GolfAimLock.Release(clubA);
            Assert.IsFalse(GolfAimLock.IsHeldByOther(clubB), "released lock must be free");
            Assert.IsTrue(GolfAimLock.TryAcquire(clubB), "next input must acquire after release");
        }
        finally
        {
            GolfAimLock.Release(clubA);
            GolfAimLock.Release(clubB);
        }
    }

    [Test]
    public void AimLock_ReleaseByNonOwnerIsNoOp()
    {
        var clubA = new object();
        var clubB = new object();
        try
        {
            Assert.IsTrue(GolfAimLock.TryAcquire(clubA));
            GolfAimLock.Release(clubB);
            Assert.IsTrue(GolfAimLock.IsHeldByOther(clubB), "non-owner release must not free the lock");
        }
        finally
        {
            GolfAimLock.Release(clubA);
        }
    }

    [Test]
    public void AimLock_NullOwnerIsRejected()
    {
        Assert.IsFalse(GolfAimLock.TryAcquire(null), "null must never acquire the lock");
    }

    [Test]
    public void Edges_RisingEdgeFiresDownOnce()
    {
        bool wasHeld = false;
        GolfInputEdges.Evaluate(false, ref wasHeld, out bool down0, out bool up0);
        Assert.IsFalse(down0 || up0, "idle must produce no edges");

        GolfInputEdges.Evaluate(true, ref wasHeld, out bool down1, out bool up1);
        Assert.IsTrue(down1, "rising edge must fire down");
        Assert.IsFalse(up1, "rising edge must not fire up");
    }

    [Test]
    public void Sphere_StaysVisibleUntilDebounceElapsesThenHides()
    {
        Assert.IsFalse(GolfSphereVisibility.ShouldHide(true, 0f, 0.25f), "no tracking time: visible");
        Assert.IsFalse(GolfSphereVisibility.ShouldHide(true, 0.24f, 0.25f), "below delay: visible");
        Assert.IsTrue(GolfSphereVisibility.ShouldHide(true, 0.25f, 0.25f), "delay elapsed: hidden");
        Assert.IsTrue(GolfSphereVisibility.ShouldHide(true, 5f, 0.25f), "long tracking: hidden");
    }

    [Test]
    public void Sphere_NeverHidesWhenAutoHideDisabled()
    {
        Assert.IsFalse(GolfSphereVisibility.ShouldHide(false, 10f, 0.25f), "disabled toggle wins over tracking");
        Assert.IsFalse(GolfSphereVisibility.ShouldHide(false, 10f, 0f), "disabled toggle wins even with zero delay");
    }

    [Test]
    public void JointAngle_StraightIsZeroFoldedIsLarge()
    {
        float straight = GolfHandPoses.JointAngle(
            new UnityEngine.Vector3(0f, 0f, 0f),
            new UnityEngine.Vector3(1f, 0f, 0f),
            new UnityEngine.Vector3(2f, 0f, 0f));
        Assert.AreEqual(0f, straight, 1e-4f, "collinear joints read 0 degrees");

        float folded = GolfHandPoses.JointAngle(
            new UnityEngine.Vector3(0f, 0f, 0f),
            new UnityEngine.Vector3(1f, 0f, 0f),
            new UnityEngine.Vector3(1f, 1f, 0f));
        Assert.AreEqual(90f, folded, 1e-4f, "right-angle fold reads 90 degrees");
    }

    [Test]
    public void Fist_RequiresAllFourFingersCurled()
    {
        Assert.IsTrue(GolfHandPoses.IsFistCurl(85f, 90f, 80f, 75f, 50f), "closed fist accepted");
        Assert.IsFalse(GolfHandPoses.IsFistCurl(85f, 90f, 80f, 10f, 50f), "extended pinky is not a fist");
        Assert.IsFalse(GolfHandPoses.IsFistCurl(80f, 5f, 5f, 5f, 50f), "precision index curl is not a fist");
        Assert.IsFalse(GolfHandPoses.IsFistCurl(5f, 5f, 5f, 5f, 50f), "open hand is not a fist");
    }

    [Test]
    public void PalmCenter_AveragesJointsAndRejectsEmpty()
    {
        var points = new System.Collections.Generic.List<UnityEngine.Vector3>
        {
            new UnityEngine.Vector3(0f, 0f, 0f),
            new UnityEngine.Vector3(2f, 0f, 0f),
        };
        Assert.IsTrue(GolfHandPoses.TryAverageCenter(points, out UnityEngine.Vector3 center));
        Assert.AreEqual(1f, center.x, 1e-5f, "palm sits between wrist and knuckles");

        Assert.IsFalse(GolfHandPoses.TryAverageCenter(
            new System.Collections.Generic.List<UnityEngine.Vector3>(), out _), "no joints: no center");
        Assert.IsFalse(GolfHandPoses.TryAverageCenter(null, out _), "null: no center");
    }

    [Test]
    public void Edges_HeldSteadyProducesNoEdgesAndFallingFiresUpOnce()
    {
        bool wasHeld = false;
        GolfInputEdges.Evaluate(true, ref wasHeld, out _, out _);
        GolfInputEdges.Evaluate(true, ref wasHeld, out bool downHeld, out bool upHeld);
        Assert.IsFalse(downHeld || upHeld, "steady hold must not repeat edges");

        GolfInputEdges.Evaluate(false, ref wasHeld, out bool downUp, out bool upUp);
        Assert.IsFalse(downUp, "falling edge must not fire down");
        Assert.IsTrue(upUp, "falling edge must fire up");

        GolfInputEdges.Evaluate(false, ref wasHeld, out bool downIdle, out bool upIdle);
        Assert.IsFalse(downIdle || upIdle, "idle after release must produce no edges");
    }
}
