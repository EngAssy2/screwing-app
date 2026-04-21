using ScrewingHub.Core.Models;

namespace ScrewingHub.Core.Interfaces;

/// <summary>
/// Service for writing grouped unit batch logs (Accept/Reject).
/// </summary>
public interface IUnitLogService
{
    /// <summary>
    /// Writes a complete assembly unit's results to a custom CSV format.
    /// </summary>
    /// <param name="status">"Accept" or "Reject"</param>
    /// <param name="batch">The collection of screw results for this unit</param>
    Task LogUnitAsync(string status, IReadOnlyList<JudgmentResult> batch);
}
