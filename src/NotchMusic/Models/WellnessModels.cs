namespace NotchMusic.Models;

public enum WellnessReminderKind
{
    Move,
    Drink
}

public class WellnessNotificationEvent
{
    public WellnessReminderKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string PrimaryActionText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
