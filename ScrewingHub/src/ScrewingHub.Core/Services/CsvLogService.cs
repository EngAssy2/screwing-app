using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.Core.Services;

/// <summary>
/// Logs screw fastening results to daily CSV files.
/// One file per day, append-only.
/// </summary>
public class CsvLogService : ICsvLogService
{
    private static readonly ILogger Logger = Log.ForContext<CsvLogService>();
    private readonly string _logFolder;
    private readonly AppSettings _settings;
    private readonly object _writeLock = new();
    private int _todayRecordCount;
    private DateTime _lastCountDate;

    public CsvLogService(AppSettings settings, string logFolder)
    {
        _settings = settings;
        _logFolder = logFolder;
        Directory.CreateDirectory(_logFolder);

        // Initialize today's count
        _lastCountDate = DateTime.Today;
        _todayRecordCount = CountRecordsInFile(GetLogFilePath(DateTime.Today, null));
    }

    public string GetLogFilePath(DateTime date, JudgmentResult? result)
    {
        string pattern = _settings.CsvFileNamePattern;
        string stationName = !string.IsNullOrEmpty(result?.StationName) ? result.StationName : "Default";
        string fileName = pattern
            .Replace("[Status]", "Raw")
            .Replace("[ModelNumber]", result?.WorkOrderNo ?? "Unknown")
            .Replace("[Date]", date.ToString("dd-MM-yyyy"))
            .Replace("[StationName]", stationName);

        // Fallback for legacy {0} style patterns if no [Status] was found
        if (fileName == pattern && pattern.Contains("{0}"))
        {
            try { fileName = string.Format(pattern, "Raw", result?.WorkOrderNo ?? "Unknown", date.ToString("dd-MM-yyyy"), stationName); }
            catch { /* Ignore format errors */ }
        }
            
        return Path.Combine(_logFolder, "Raw", fileName);
    }

    public int GetTodayRecordCount()
    {
        // Reset counter if day has changed
        if (DateTime.Today != _lastCountDate)
        {
            _lastCountDate = DateTime.Today;
            _todayRecordCount = CountRecordsInFile(GetLogFilePath(DateTime.Today, null));
        }
        return _todayRecordCount;
    }

    public int GetTodayUnitCount(string status)
    {
        try
        {
            var dirPath = Path.Combine(_logFolder, status);
            if (!Directory.Exists(dirPath)) return 0;

            var dateStr = DateTime.Today.ToString("dd-MM-yyyy");
            var files = Directory.GetFiles(dirPath, $"*{dateStr}*.csv");

            int totalCount = 0;
            foreach (var file in files)
            {
                totalCount += CountUnitRecordsInFile(file);
            }
            return totalCount;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to count unit logs for {Status}", status);
            return 0;
        }
    }

    private int CountUnitRecordsInFile(string filePath)
    {
        if (!File.Exists(filePath)) return 0;
        try
        {
            var lines = File.ReadAllLines(filePath);
            // Unit log has headers:
            // 1: Title
            // 2: ID,Description...
            // ... Pt rows ...
            // Empty Line
            // Column Headers
            // Data Rows (Starting with dd/MM/yyyy)
            
            return lines.Count(l => l.Contains("/") && l.Contains(":") && char.IsDigit(l.TrimStart()[0]));
        }
        catch { return 0; }
    }

    public async Task LogResultAsync(JudgmentResult result)
    {
        try
        {
            var folder = Path.Combine(_logFolder, "Raw");
            Directory.CreateDirectory(folder);

            var filePath = GetLogFilePath(DateTime.Today, result);
            bool fileExists = File.Exists(filePath);

            // Reset counter if day changed
            if (DateTime.Today != _lastCountDate)
            {
                _lastCountDate = DateTime.Today;
                _todayRecordCount = 0;
            }

            _todayRecordCount++;
            result.RecordNumber = _todayRecordCount;

            lock (_writeLock)
            {
                using var writer = new StreamWriter(filePath, append: true, Encoding.UTF8);
                using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = !fileExists
                });

                if (!fileExists)
                {
                    csv.WriteHeader<CsvLogRecord>();
                    csv.NextRecord();
                }

                var record = MapToLogRecord(result);
                csv.WriteRecord(record);
                csv.NextRecord();
                csv.Flush();
                writer.Flush();
            }

            Logger.Debug("Logged record #{No} to {File}", result.RecordNumber, filePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to write CSV log record");
            throw;
        }

        await Task.CompletedTask;
    }

    public async Task<List<JudgmentResult>> ReadLogAsync(DateTime date)
    {
        var results = new List<JudgmentResult>();
        var folder = Path.Combine(_logFolder, "Raw");

        if (!Directory.Exists(folder))
            return results;

        var dateStr = date.ToString("dd-MM-yyyy");
        // Looking for Raw_*_dd-MM-yyyy_*.csv
        var files = Directory.GetFiles(folder, $"Raw_*_{dateStr}_*.csv");

        foreach (var filePath in files)
        {
            try
            {
                using var reader = new StreamReader(filePath, Encoding.UTF8);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null
                });

                var records = csv.GetRecords<CsvLogRecord>().ToList();
                foreach (var rec in records)
                {
                    results.Add(MapToJudgmentResult(rec));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to read CSV log file: {File}", filePath);
            }
        }

        return await Task.FromResult(results.OrderBy(r => r.Timestamp).ToList());
    }

    public int GetUnitCountInRange(DateTime start, DateTime end, string status)
    {
        try
        {
            var dirPath = Path.Combine(_logFolder, status);
            if (!Directory.Exists(dirPath)) return 0;

            int totalCount = 0;
            // Iterate day by day from start to end to pick up all relevant files
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                var dateStr = date.ToString("dd-MM-yyyy");
                var files = Directory.GetFiles(dirPath, $"*{dateStr}*.csv");

                foreach (var file in files)
                {
                    int fileCount = CountUnitRecordsInRange(file, start, end);
                    totalCount += fileCount;
                    Logger.Debug("Counted {Count} units in {File} for range {Start}-{End}", fileCount, Path.GetFileName(file), start, end);
                }
            }
            return totalCount;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to count unit logs in range for {Status}", status);
            return 0;
        }
    }

    public async Task<List<JudgmentResult>> ReadLogRangeAsync(DateTime start, DateTime end)
    {
        var results = new List<JudgmentResult>();
        var folder = Path.Combine(_logFolder, "Raw");

        if (!Directory.Exists(folder))
            return results;

        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            var dateStr = date.ToString("dd-MM-yyyy");
            var files = Directory.GetFiles(folder, $"Raw_*_{dateStr}_*.csv");

            foreach (var filePath in files)
            {
                try
                {
                    using var reader = new StreamReader(filePath, Encoding.UTF8);
                    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HeaderValidated = null,
                        MissingFieldFound = null
                    });

                    var records = csv.GetRecords<CsvLogRecord>().ToList();
                    foreach (var rec in records)
                    {
                        var result = MapToJudgmentResult(rec);
                        if (result.Timestamp >= start && result.Timestamp < end)
                        {
                            results.Add(result);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to read CSV log file: {File}", filePath);
                }
            }
        }

        return await Task.FromResult(results.OrderBy(r => r.Timestamp).ToList());
    }

    private int CountUnitRecordsInRange(string filePath, DateTime start, DateTime end)
    {
        if (!File.Exists(filePath)) return 0;
        try
        {
            // Use FileStream with FileShare.ReadWrite to handle open files
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            
            int count = 0;
            string[] formats = { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm" };
            
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Format check: contains date/time separators and starts with a digit
                string trimmed = line.Trim();
                if (trimmed.Contains("/") && trimmed.Contains(":") && char.IsDigit(trimmed[0]))
                {
                    var parts = trimmed.Split(',');
                    if (parts.Length > 0 && DateTime.TryParseExact(parts[0].Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                    {
                        if (timestamp >= start && timestamp < end)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error counting records in {File}", filePath);
            return 0;
        }
    }

    private int CountRecordsInFile(string filePath)
    {
        if (!File.Exists(filePath)) return 0;
        try
        {
            var lines = File.ReadAllLines(filePath);
            return Math.Max(0, lines.Length - 1); // Subtract header
        }
        catch { return 0; }
    }

    private static CsvLogRecord MapToLogRecord(JudgmentResult r) => new()
    {
        No = r.RecordNumber,
        Timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
        Model = r.ModelName,
        Channel = $"CH{r.Channel}",
        ScrewName = r.ScrewName,
        ScrewNo = r.ScrewProgress,
        RawCurrentValue = r.RawCurrentValue,
        ConvertedTorque = Math.Round(r.ConvertedTorque, 4),
        TorqueFactor = r.TorqueConversionFactor, // Added for data integrity
        ScrewTimeMs = r.ScrewTimeMs,
        UpperLimit = r.CurrentUpperLimit,
        LowerLimit = r.CurrentLowerLimit,
        TimeUpperLimit = r.TimeUpperLimitMs,
        TimeLowerLimit = r.TimeLowerLimitMs,
        Judgment = r.Judgment.ToString(),
        PlcSendStatus = r.PlcStatus.ToString(),
        OperatorId = r.OperatorId,
        WorkOrderNo = r.WorkOrderNo,
        Remarks = r.Remarks
    };

    private static JudgmentResult MapToJudgmentResult(CsvLogRecord r)
    {
        var result = new JudgmentResult
        {
            RecordNumber = r.No,
            Timestamp = DateTime.TryParse(r.Timestamp, out var dt) ? dt : DateTime.MinValue,
            ModelName = r.Model,
            Channel = int.TryParse(r.Channel?.Replace("CH", ""), out var ch) ? ch : 0,
            ScrewName = r.ScrewName,
            RawCurrentValue = r.RawCurrentValue,
            ScrewTimeMs = r.ScrewTimeMs,
            CurrentUpperLimit = r.UpperLimit,
            CurrentLowerLimit = r.LowerLimit,
            TimeUpperLimitMs = r.TimeUpperLimit,
            TimeLowerLimitMs = r.TimeLowerLimit,
            Judgment = Enum.TryParse<JudgmentStatus>(r.Judgment, out var j) ? j : JudgmentStatus.Error,
            PlcStatus = Enum.TryParse<PlcSendStatus>(r.PlcSendStatus, out var p) ? p : PlcSendStatus.Pending,
            OperatorId = r.OperatorId,
            WorkOrderNo = r.WorkOrderNo,
            Remarks = r.Remarks,
            TorqueConversionFactor = r.TorqueFactor
        };

        // Fallback for missing factor in older logs
        if (result.TorqueConversionFactor == 0 && r.RawCurrentValue > 0)
        {
            result.TorqueConversionFactor = r.ConvertedTorque / r.RawCurrentValue;
        }

        // Restore ScrewNumber and TotalScrews from "1/5" format
        if (!string.IsNullOrEmpty(r.ScrewNo))
        {
            var parts = r.ScrewNo.Split('/');
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out var sn)) result.ScrewNumber = sn;
                if (int.TryParse(parts[1], out var ts)) result.TotalScrews = ts;
            }
        }

        return result;
    }
}

/// <summary>CSV record shape for CsvHelper serialization.</summary>
public class CsvLogRecord
{
    public int No { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ScrewName { get; set; } = string.Empty;
    public string ScrewNo { get; set; } = string.Empty;
    [Name("RawCurrentValue", "Value (Raw)")]
    public int RawCurrentValue { get; set; }

    [Name("ConvertedTorque", "Torque (kgf·cm)")]
    public double ConvertedTorque { get; set; }

    [Optional] [Name("TorqueFactor")]
    public double TorqueFactor { get; set; }

    [Name("ScrewTimeMs", "Time (ms)")]
    public int ScrewTimeMs { get; set; }

    [Name("UpperLimit", "Upper Limit (kgf·cm)")]
    public int UpperLimit { get; set; }

    [Name("LowerLimit", "Lower Limit (kgf·cm)")]
    public int LowerLimit { get; set; }

    [Name("TimeUpperLimit", "Time Upper (ms)")]
    public int TimeUpperLimit { get; set; }

    [Name("TimeLowerLimit", "Time Lower (ms)")]
    public int TimeLowerLimit { get; set; }
    public string Judgment { get; set; } = string.Empty;
    public string PlcSendStatus { get; set; } = string.Empty;
    public string OperatorId { get; set; } = string.Empty;
    public string WorkOrderNo { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}
