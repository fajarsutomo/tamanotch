using System;
using System.Threading;
using System.Windows.Threading;
using NotchMusic.ViewModels;
using Xunit;

namespace NotchMusic.Tests;

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
