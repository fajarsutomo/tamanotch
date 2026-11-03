using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NotchMusic.Views;

public partial class NotchEyes : System.Windows.Controls.UserControl
{
    private static readonly TimeSpan BlinkCycle = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan LookCycle = TimeSpan.FromSeconds(7);

    public NotchEyes()
    {
        InitializeComponent();
        IsVisibleChanged += (s, e) =>
        {
            if (e.NewValue is true) StartAnimations(); else StopAnimations();
        };
        Unloaded += (s, e) => StopAnimations();
    }

    // Animasi hanya berjalan saat terlihat agar tidak memakai CPU ketika notch sedang di-hover/disembunyikan.
    private void StartAnimations()
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        var blink = CreateBlinkAnimation();
        LeftBlink.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
        RightBlink.BeginAnimation(ScaleTransform.ScaleYProperty, blink);

        var look = CreateLookAnimation();
        LeftLook.BeginAnimation(TranslateTransform.XProperty, look);
        RightLook.BeginAnimation(TranslateTransform.XProperty, look);
    }

    private void StopAnimations()
    {
        LeftBlink.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        RightBlink.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        LeftLook.BeginAnimation(TranslateTransform.XProperty, null);
        RightLook.BeginAnimation(TranslateTransform.XProperty, null);
    }

    private static DoubleAnimationUsingKeyFrames CreateBlinkAnimation()
    {
        var anim = new DoubleAnimationUsingKeyFrames { Duration = BlinkCycle, RepeatBehavior = RepeatBehavior.Forever };
        // Mata terbuka lama, lalu kedip ganda cepat.
        AddFrame(anim, 0.00, 1);
        AddFrame(anim, 4.00, 1);
        AddFrame(anim, 4.12, 0.08);
        AddFrame(anim, 4.28, 1);
        AddFrame(anim, 4.62, 1);
        AddFrame(anim, 4.72, 0.08);
        AddFrame(anim, 4.88, 1);
        AddFrame(anim, 6.00, 1);
        return anim;
    }

    private static DoubleAnimationUsingKeyFrames CreateLookAnimation()
    {
        var anim = new DoubleAnimationUsingKeyFrames { Duration = LookCycle, RepeatBehavior = RepeatBehavior.Forever };
        // Melirik ke kiri, lalu ke kanan, lalu kembali ke tengah.
        AddFrame(anim, 0.0, 0, easing: false);
        AddFrame(anim, 1.2, 0);
        AddFrame(anim, 1.7, -2.2);
        AddFrame(anim, 3.0, -2.2);
        AddFrame(anim, 3.5, 2.2);
        AddFrame(anim, 5.0, 2.2);
        AddFrame(anim, 5.6, 0);
        AddFrame(anim, 7.0, 0);
        return anim;
    }

    private static void AddFrame(DoubleAnimationUsingKeyFrames anim, double seconds, double value, bool easing = true)
    {
        var time = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds));
        anim.KeyFrames.Add(easing
            ? new EasingDoubleKeyFrame(value, time) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } }
            : new EasingDoubleKeyFrame(value, time));
    }
}
