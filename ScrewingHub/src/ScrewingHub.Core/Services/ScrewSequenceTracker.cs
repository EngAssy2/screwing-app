using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.Core.Services;

/// <summary>
/// Tracks screw fastening progress per unit for the current product model.
/// </summary>
public class ScrewSequenceTracker
{
    private static readonly ILogger Logger = Log.ForContext<ScrewSequenceTracker>();

    private int _currentScrewNumber;
    private int _totalScrews;
    private string _modelName = string.Empty;
    private readonly List<JudgmentResult> _completedScrews = new();

    public int CurrentScrewNumber => _currentScrewNumber;
    public int TotalScrews => _totalScrews;
    public bool AllScrewsComplete => _currentScrewNumber > _totalScrews;
    public IReadOnlyList<JudgmentResult> CompletedScrews => _completedScrews.AsReadOnly();
    public int OkCount => _completedScrews.Count(s => s.Judgment == JudgmentStatus.OK);
    public int NgCount => _completedScrews.Count(s => s.Judgment == JudgmentStatus.NG);

    /// <summary>
    /// Start tracking a new unit with the given product model.
    /// </summary>
    public void StartNewUnit(ProductModel model)
    {
        _modelName = model.ModelName;
        _totalScrews = model.TotalScrewCount;
        _currentScrewNumber = 1;
        _completedScrews.Clear();
        Logger.Information("New unit started: {Model} ({TotalScrews} screws)", _modelName, _totalScrews);
    }

    /// <summary>
    /// Record a completed screw. Returns the screw number that was just completed.
    /// Only advances to next screw if the result is OK.
    /// </summary>
    public int RecordScrew(JudgmentResult result)
    {
        int screwNum = _currentScrewNumber;
        result.ScrewNumber = screwNum;
        result.TotalScrews = _totalScrews;

        _completedScrews.Add(result);

        if (result.Judgment == JudgmentStatus.OK)
        {
            _currentScrewNumber++;
            Logger.Information("Screw {No}/{Total} OK — advancing to next", screwNum, _totalScrews);
        }
        else
        {
            Logger.Warning("Screw {No}/{Total} NG — NOT advancing (retry needed)", screwNum, _totalScrews);
        }

        return screwNum;
    }

    /// <summary>
    /// Reset the tracker for a new unit (same model).
    /// </summary>
    public void ResetForNextUnit()
    {
        _currentScrewNumber = 1;
        _completedScrews.Clear();
        Logger.Information("Tracker reset for next unit: {Model}", _modelName);
    }
}
