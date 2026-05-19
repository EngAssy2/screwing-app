using ScrewingHub.Core.Models;

namespace ScrewingHub.Core.Interfaces;

/// <summary>
/// Service for logging screw fastening data to daily CSV files.
/// </summary>
public interface ICsvLogService
{
    /// <summary>
    /// Append a judgment result to today's CSV log file.
    /// Creates the file with headers if it doesn't exist.
    /// </summary>
    Task LogResultAsync(JudgmentResult result);

    /// <summary>
    /// Get the file path for a specific date's log file.
    /// </summary>
    string GetLogFilePath(DateTime date, JudgmentResult? result = null);

    /// <summary>
    /// Read all records from a specific date's log file.
    /// </summary>
    Task<List<JudgmentResult>> ReadLogAsync(DateTime date);

    /// <summary>
    /// Get today's record count (for numbering).
    /// </summary>
    int GetTodayRecordCount();
    
    /// <summary>
    /// Gets the count of finalized units for today (Accept or Reject).
    /// </summary>
    int GetTodayUnitCount(string status);

    /// <summary>
    /// Gets the count of finalized units within a specific time range.
    /// Handles cross-day queries.
    /// </summary>
    int GetUnitCountInRange(DateTime start, DateTime end, string status);

    /// <summary>
    /// Reads all records within a specific time range.
    /// Handles cross-day queries.
    /// </summary>
    Task<List<JudgmentResult>> ReadLogRangeAsync(DateTime start, DateTime end);
}
