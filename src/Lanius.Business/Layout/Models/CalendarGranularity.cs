namespace Lanius.Business.Layout.Models;

/// <summary>
/// Defines calendar granularity for time-based commit grouping.
/// </summary>
public enum CalendarGranularity
{
    /// <summary>
    /// Group commits by day (00:00 to 23:59 of same date).
    /// </summary>
    Day,

    /// <summary>
    /// Group commits by ISO week (Monday to Sunday).
    /// </summary>
    Week,

    /// <summary>
    /// Group commits by month (first to last day of month).
    /// </summary>
    Month,

    /// <summary>
    /// Group commits by year (January 1 to December 31).
    /// </summary>
    Year
}
