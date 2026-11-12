using System;
using System.Threading;
using System.Windows.Threading;
using TamaNotch.ViewModels;
using Xunit;

namespace TamaNotch.Tests;

/// <summary>Island yang terbuka karena hover harus kembali ke notch idle setelah kursor pergi.</summary>
public class HoverCollapseTests
{
    private static MainViewModel CreateHovered()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.OnMouseEnter();
        Assert.True(vm.ShowComponentIcons);
        return vm;
    }

    [Fact]
    public void CollapseHoverIfIdle_AfterMouseLeaves_ReturnsToIdle()
    {
        var vm = CreateHovered();

        vm.OnMouseLeave();
        vm.CollapseHoverIfIdle();

        Assert.False(vm.IsHovering);
        Assert.False(vm.ShowComponentIcons);
        Assert.True(vm.ShowIdleEyes);
    }

    [Fact]
    public void CollapseHoverIfIdle_WhileMouseIsStillOverNotch_StaysOpen()
    {
        var vm = CreateHovered();

        vm.CollapseHoverIfIdle();

        Assert.True(vm.IsHovering);
        Assert.True(vm.ShowComponentIcons);
    }

    [Fact]
    public void CollapseHoverIfIdle_WhenPinned_StaysOpen()
    {
        var vm = CreateHovered();
        vm.IsPinned = true;
        vm.OnMouseLeave();

        vm.CollapseHoverIfIdle();

        Assert.True(vm.IsHovering);
        Assert.True(vm.ShowComponentIcons);
    }

    [Fact]
    public void CollapseHoverIfIdle_WhileComponentsMenuIsOpen_StaysOpen()
    {
        var vm = CreateHovered();
        vm.IsComponentsMenuOpen = true;
        vm.OnMouseLeave();

        vm.CollapseHoverIfIdle();

        Assert.True(vm.IsHovering);

        vm.IsComponentsMenuOpen = false;
        vm.CollapseHoverIfIdle();
        Assert.False(vm.IsHovering);
    }

    [Fact]
    public void WithAnimationSubscriber_CollapseWaitsForTheViewToCommit()
    {
        var vm = CreateHovered();
        Action? commit = null;
        vm.RequestHoverCollapseAnimation += c => commit = c;
        vm.OnMouseLeave();

        vm.CollapseHoverIfIdle();

        Assert.True(vm.IsHovering);          // masih terbuka selama animasi
        Assert.NotNull(commit);

        commit!();
        Assert.False(vm.IsHovering);
        Assert.True(vm.ShowIdleEyes);
    }

    [Fact]
    public void CommitAfterTheMouseReturned_KeepsTheIslandOpen()
    {
        var vm = CreateHovered();
        Action? commit = null;
        int cancelled = 0;
        vm.RequestHoverCollapseAnimation += c => commit = c;
        vm.HoverCollapseAnimationCancelled += () => cancelled++;
        vm.OnMouseLeave();
        vm.CollapseHoverIfIdle();

        vm.OnMouseEnter();   // kursor kembali selagi island menciut

        Assert.Equal(1, cancelled);
        commit!();
        Assert.True(vm.IsHovering);
    }

    [Fact]
    public void IdleCapsuleEstimate_UsesConfiguredFrame_OrFollowsTheEyes()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.IdleFrameAutoSize = false;
        settings.CurrentSettings.IdleFrameWidth = 120;
        settings.CurrentSettings.IdleFrameHeight = 40;
        var vm = ViewModelTestFactory.CreateMain(settings);

        Assert.Equal((120.0, 40.0), vm.EstimateIdleCapsuleSize());

        settings.CurrentSettings.IdleFrameAutoSize = true;
        var auto = vm.EstimateIdleCapsuleSize();
        Assert.InRange(auto.Width, 40, 120);
        Assert.InRange(auto.Height, 20, 60);
    }

    // --- Dengan timer sungguhan pada dispatcher ---

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var stop = new DispatcherTimer(duration, DispatcherPriority.Normal, (s, e) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        stop.Start();
        Dispatcher.PushFrame(frame);
        stop.Stop();
    }

    [Fact]
    public void Timer_AfterDelayWithoutMouse_CollapsesIsland()
    {
        RunSta(() =>
        {
            var vm = CreateHovered();
            vm.HoverCollapseDelay = TimeSpan.FromMilliseconds(40);

            vm.OnMouseLeave();
            Assert.True(vm.IsHovering);          // belum, masih menunggu jeda

            Pump(TimeSpan.FromMilliseconds(300));

            Assert.False(vm.IsHovering);
            Assert.False(vm.ShowComponentIcons);
        });
    }

    [Fact]
    public void Timer_MouseReturnsBeforeDelay_KeepsIslandOpen()
    {
        RunSta(() =>
        {
            var vm = CreateHovered();
            vm.HoverCollapseDelay = TimeSpan.FromMilliseconds(120);

            vm.OnMouseLeave();
            Pump(TimeSpan.FromMilliseconds(30));
            vm.OnMouseEnter();                    // kursor kembali sebelum jeda habis
            Pump(TimeSpan.FromMilliseconds(350));

            Assert.True(vm.IsHovering);
            Assert.True(vm.ShowComponentIcons);
        });
    }

    [Fact]
    public void Timer_UnpinningWhileMouseIsAway_CollapsesIsland()
    {
        RunSta(() =>
        {
            var vm = CreateHovered();
            vm.HoverCollapseDelay = TimeSpan.FromMilliseconds(40);
            vm.IsPinned = true;
            vm.OnMouseLeave();
            Pump(TimeSpan.FromMilliseconds(150));
            Assert.True(vm.IsHovering);           // dipin: tetap terbuka

            vm.IsPinned = false;
            Pump(TimeSpan.FromMilliseconds(300));

            Assert.False(vm.IsHovering);
        });
    }
}
