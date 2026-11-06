namespace NotchMusic.Models;

public enum WellnessReminderKind
{
    Move,
    Drink,

    /// <summary>Sapaan setelah kembali dari idle lama; hanya tampil di notch.</summary>
    Welcome
}

public class WellnessNotificationEvent
{
    public WellnessReminderKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string PrimaryActionText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
