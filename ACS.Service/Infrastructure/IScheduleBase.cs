namespace ACS.Service.Infrastructure;

/// <summary>
/// Schedule types for background services
/// </summary>
public enum ScheduleType
{
    Daily,
    Weekly,
    Monthly,
    Interval
}

/// <summary>
/// Base interface for schedule configuration
/// </summary>
public interface IScheduleBase
{
    bool Enabled { get; set; }
    ScheduleType ScheduleType { get; set; }
    string DailyAt { get; set; }
    string WeeklyOn { get; set; }
    string WeeklyAt { get; set; }
    int MonthlyDay { get; set; }
    string MonthlyAt { get; set; }
    int IntervalHours { get; set; }
}