using ScrewingHub.Core.Models;

namespace ScrewingHub.Core.Interfaces;

/// <summary>
/// Service for evaluating OK/NG judgment on screw fastening data.
/// </summary>
public interface IJudgmentService
{
    /// <summary>
    /// Evaluate a single screw fastening against the current model's thresholds.
    /// </summary>
    JudgmentResult Evaluate(ScrewData data, ProductModel model, int screwNumber);
}
