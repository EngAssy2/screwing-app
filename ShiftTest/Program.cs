using System.Globalization;
using System.Text;

namespace ReproduceShiftIssue;

public class Program
{
    public static void Main()
    {
        Console.WriteLine("Starting Shift Logic Test...");
        
        var logFolder = Path.Combine(Directory.GetCurrentDirectory(), "TestLogs");
        if (Directory.Exists(logFolder)) Directory.Delete(logFolder, true);
        Directory.CreateDirectory(logFolder);

        var now = DateTime.Now;
        var dateStr = now.ToString("dd-MM-yyyy");
        
        // Scenario 1: Create a Morning Shift Log (8:00 - 20:30)
        // Let's assume today is May 7, 2026
        var acceptFolder = Path.Combine(logFolder, "Accept");
        Directory.CreateDirectory(acceptFolder);
        
        var logContent = new StringBuilder();
        logContent.WriteLine("DUPLO Journey Screwing Process Station 1 : WO123");
        logContent.WriteLine("ID,Description,LSL,USL,Unit");
        logContent.WriteLine("Pt1,Screwing 1,1.000,2.000,kgf·cm");
        logContent.WriteLine("");
        logContent.WriteLine("Date and Time,Duration(sec),Pt1,Result,Remarks");
        logContent.WriteLine($"{now.Date.AddHours(9).ToString("dd/MM/yyyy HH:mm:ss")},0.500000,1.50,ACCEPT,");
        logContent.WriteLine($"{now.Date.AddHours(10).ToString("dd/MM/yyyy HH:mm:ss")},0.500000,1.50,ACCEPT,");
        
        var filePath = Path.Combine(acceptFolder, $"Accept_WO123_{dateStr}_Station1.csv");
        File.WriteAllText(filePath, logContent.ToString(), Encoding.UTF8);
        
        // Scenario 2: Create a Reject Log
        var rejectFolder = Path.Combine(logFolder, "Reject");
        Directory.CreateDirectory(rejectFolder);
        var rejectContent = new StringBuilder();
        rejectContent.WriteLine("DUPLO Journey Screwing Process Station 1 : WO123");
        rejectContent.WriteLine("ID,Description,LSL,USL,Unit");
        rejectContent.WriteLine("Pt1,Screwing 1,1.000,2.000,kgf·cm");
        rejectContent.WriteLine("");
        rejectContent.WriteLine("Date and Time,Duration(sec),Pt1,Result,Remarks");
        rejectContent.WriteLine($"{now.Date.AddHours(11).ToString("dd/MM/yyyy HH:mm:ss")},0.500000,1.50,REJECT,Test NG");
        
        var rejectPath = Path.Combine(rejectFolder, $"Reject_WO123_{dateStr}_Station1.csv");
        File.WriteAllText(rejectPath, rejectContent.ToString(), Encoding.UTF8);

        // Test the Counting Logic
        var morningStart = now.Date.AddHours(8);
        var nightStart = now.Date.AddHours(20).AddMinutes(30);
        
        Console.WriteLine($"Testing Range: {morningStart} to {nightStart}");
        
        int acceptCount = CountUnitRecordsInRange(filePath, morningStart, nightStart);
        int rejectCount = CountUnitRecordsInRange(rejectPath, morningStart, nightStart);
        
        Console.WriteLine($"Accept Count: {acceptCount} (Expected: 2)");
        Console.WriteLine($"Reject Count: {rejectCount} (Expected: 1)");

        if (acceptCount == 2 && rejectCount == 1)
        {
            Console.WriteLine("SUCCESS: Logic works for basic scenario.");
        }
        else
        {
            Console.WriteLine("FAILURE: Logic failed basic scenario.");
        }
        
        // Scenario 3: Test across midnight (Night Shift)
        // Night shift May 6 20:30 to May 7 08:00
        var nightStartPrev = now.Date.AddDays(-1).AddHours(20).AddMinutes(30);
        var morningStartToday = now.Date.AddHours(8);
        
        Console.WriteLine($"Testing Night Shift Range: {nightStartPrev} to {morningStartToday}");
        
        var nightLogPath = Path.Combine(acceptFolder, $"Accept_WO999_{now.Date.AddDays(-1).ToString("dd-MM-yyyy")}_Station1.csv");
        var nightLogContent = new StringBuilder();
        nightLogContent.WriteLine("Header...");
        nightLogContent.WriteLine("ID,Description,LSL,USL,Unit");
        nightLogContent.WriteLine("Pt1,Screwing 1,1.000,2.000,kgf·cm");
        nightLogContent.WriteLine("");
        nightLogContent.WriteLine("Date and Time,Duration(sec),Pt1,Result,Remarks");
        nightLogContent.WriteLine($"{now.Date.AddDays(-1).AddHours(22).ToString("dd/MM/yyyy HH:mm:ss")},0.500000,1.50,ACCEPT,"); // May 6 22:00
        nightLogContent.WriteLine($"{now.Date.AddHours(2).ToString("dd/MM/yyyy HH:mm:ss")},0.500000,1.50,ACCEPT,"); // May 7 02:00
        File.WriteAllText(nightLogPath, nightLogContent.ToString(), Encoding.UTF8);
        
        // Current logic in GetUnitCountInRange iterates through days
        int totalNightCount = 0;
        for (var d = nightStartPrev.Date; d <= morningStartToday.Date; d = d.AddDays(1))
        {
            var dStr = d.ToString("dd-MM-yyyy");
            var files = Directory.GetFiles(acceptFolder, $"*{dStr}*.csv");
            foreach (var f in files)
            {
                totalNightCount += CountUnitRecordsInRange(f, nightStartPrev, morningStartToday);
            }
        }
        
        Console.WriteLine($"Night Shift Total Count: {totalNightCount} (Expected: 2)");
        if (totalNightCount == 2)
        {
            Console.WriteLine("SUCCESS: Night shift logic works.");
        }
        else
        {
            Console.WriteLine("FAILURE: Night shift logic failed.");
        }
    }

    private static int CountUnitRecordsInRange(string filePath, DateTime start, DateTime end)
    {
        if (!File.Exists(filePath)) return 0;
        try
        {
            var lines = File.ReadAllLines(filePath);
            int count = 0;
            string[] formats = { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm" };
            foreach (var line in lines)
            {
                if (line.Contains("/") && line.Contains(":") && char.IsDigit(line.TrimStart()[0]))
                {
                    var parts = line.Split(',');
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
        catch { return 0; }
    }
}

public static class Extensions
{
    public static void WriteLine(this StringBuilder sb, string line)
    {
        sb.Append(line + Environment.NewLine);
    }
}
