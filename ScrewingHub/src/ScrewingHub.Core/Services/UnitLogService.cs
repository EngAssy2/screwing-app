using System.Globalization;
using System.Text;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.Core.Services;

public class UnitLogService : IUnitLogService
{
    private static readonly ILogger Logger = Log.ForContext<UnitLogService>();
    private readonly AppSettings _settings;
    private readonly object _writeLock = new();

    public UnitLogService(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task LogUnitAsync(string status, IReadOnlyList<JudgmentResult> batch)
    {
        if (batch.Count == 0) return;

        var first = batch[0];
        // Calculate dynamic filename
        // Ex: [Status]_[ModelNumber]_[Date]_[StationName].csv
        var stationName = !string.IsNullOrEmpty(first.StationName) ? first.StationName : "Default";
        var fileName = _settings.CsvFileNamePattern
            .Replace("[Status]", status)
            .Replace("[ModelNumber]", first.WorkOrderNo)
            .Replace("[Date]", DateTime.Today.ToString("dd-MM-yyyy"))
            .Replace("[StationName]", stationName);

        // Fallback for legacy {0} style patterns
        if (fileName == _settings.CsvFileNamePattern && _settings.CsvFileNamePattern.Contains("{0}"))
        {
            try { fileName = string.Format(_settings.CsvFileNamePattern, status, first.WorkOrderNo, DateTime.Today.ToString("dd-MM-yyyy"), stationName); }
            catch { /* Ignore format errors */ }
        }

        // Group files into Accept / Reject folders for neatness, or just root
        var dirPath = Path.Combine(_settings.CsvLogFolder, status);
        Directory.CreateDirectory(dirPath);

        var filePath = Path.Combine(dirPath, fileName);
        bool fileExists = File.Exists(filePath);

        try
        {
            lock (_writeLock)
            {
                using var writer = new StreamWriter(filePath, append: true, Encoding.UTF8);

                // If new file, write headers
                if (!fileExists)
                {
                    writer.WriteLine($"DUPLO Journey Screwing Process {stationName} : {first.WorkOrderNo}");
                    writer.WriteLine("ID,Description,LSL,USL,Unit");

                    // parameter specs based on total screws
                    for (int i = 1; i <= first.TotalScrews; i++)
                    {
                        var pt = batch.FirstOrDefault(x => x.ScrewNumber == i) ?? first;
                        var unit = pt.TorqueConversionFactor > 0 ? "kgf·cm" : "Raw";
                        writer.WriteLine($"Pt{i},Screwing {i},{pt.TorqueLowerLimit:F3},{pt.TorqueUpperLimit:F3},{unit}");
                    }
                    
                    writer.WriteLine("");

                    // header row for data
                    var sbHeader = new StringBuilder();
                    sbHeader.Append("Date and Time,Duration(sec)");
                    for (int i = 1; i <= first.TotalScrews; i++)
                    {
                        sbHeader.Append($",Pt{i}");
                    }
                    sbHeader.Append(",Result,Remarks");
                    writer.WriteLine(sbHeader.ToString());
                }

                // Data Row
                var sbData = new StringBuilder();
                sbData.Append($"{DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")},");

                double totalDuration = batch.Sum(x => x.ScrewTimeMs) / 1000.0;
                sbData.Append($"{totalDuration:F6}");

                for (int i = 1; i <= first.TotalScrews; i++)
                {
                    var screw = batch.FirstOrDefault(x => x.ScrewNumber == i);
                    if (screw != null)
                    {
                        var val = screw.TorqueConversionFactor > 0 ? $"{screw.ConvertedTorque:F2}" : $"{screw.RawCurrentValue}";
                        sbData.Append($",{val}");
                    }
                    else
                    {
                        sbData.Append(",-");
                    }
                }

                var remarks = string.Join("; ", batch.Select(x => x.Remarks).Where(r => !string.IsNullOrEmpty(r)).Distinct());
                sbData.Append($",{status.ToUpper()},{remarks}");
                writer.WriteLine(sbData.ToString());
                writer.Flush();
            }

            Logger.Debug("Logged {Status} unit to {File}", status, filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to write grouped unit log");
        }

        await Task.CompletedTask;
    }
}
