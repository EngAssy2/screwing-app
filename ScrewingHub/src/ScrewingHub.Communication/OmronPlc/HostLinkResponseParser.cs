using Serilog;

namespace ScrewingHub.Communication.OmronPlc;

/// <summary>
/// Parses Omron Host Link response frames.
/// Response format: @{NodeAddr}{Command}{EndCode}{Data}{FCS}*\r
/// </summary>
public class HostLinkResponseParser
{
    private static readonly ILogger Logger = Log.ForContext<HostLinkResponseParser>();

    /// <summary>
    /// Parse a Host Link response frame.
    /// </summary>
    /// <param name="response">Raw response string from PLC.</param>
    /// <returns>Parsed response or null if invalid.</returns>
    public HostLinkResponse? Parse(string response)
    {
        try
        {
            if (string.IsNullOrEmpty(response))
                return null;

            // Remove trailing whitespace/CR
            response = response.TrimEnd('\r', '\n', ' ');

            // Must start with @ and end with *
            if (!response.StartsWith("@") || !response.Contains("*"))
                return null;

            // Split at * to separate data and FCS
            var starPos = response.LastIndexOf('*');
            var fcsStr = response.Substring(starPos - 2, 2);
            var frameContent = response.Substring(0, starPos - 2);

            // Verify FCS
            var expectedFcs = FcsCalculator.Calculate(frameContent);
            if (fcsStr != expectedFcs)
            {
                Logger.Warning("FCS mismatch: expected {Expected}, got {Actual}", expectedFcs, fcsStr);
                return new HostLinkResponse
                {
                    IsValid = false,
                    ErrorMessage = "FCS mismatch"
                };
            }

            // Parse fields: @{node:2}{command:2}{endCode:2}{data}
            var nodeAddr = int.Parse(frameContent.Substring(1, 2));
            var command = frameContent.Substring(3, 2);
            var endCode = frameContent.Substring(5, 2);
            var data = frameContent.Length > 7 ? frameContent.Substring(7) : string.Empty;

            return new HostLinkResponse
            {
                IsValid = true,
                NodeAddress = nodeAddr,
                Command = command,
                EndCode = endCode,
                Data = data,
                IsSuccess = endCode == "00",
                ErrorMessage = endCode != "00" ? GetErrorDescription(endCode) : null
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to parse Host Link response: '{Response}'", response);
            return null;
        }
    }

    /// <summary>
    /// Parse DM word values from response data string.
    /// Each word is 4 hex characters.
    /// </summary>
    public int[] ParseDmValues(string data)
    {
        if (string.IsNullOrEmpty(data)) return Array.Empty<int>();

        var values = new List<int>();
        for (int i = 0; i + 4 <= data.Length; i += 4)
        {
            var hexWord = data.Substring(i, 4);
            if (int.TryParse(hexWord, System.Globalization.NumberStyles.HexNumber, null, out int value))
            {
                values.Add(value);
            }
        }
        return values.ToArray();
    }

    private static string GetErrorDescription(string endCode)
    {
        return endCode switch
        {
            "00" => "Normal completion",
            "01" => "Not executable in RUN mode",
            "02" => "Not executable in MONITOR mode",
            "04" => "Address over",
            "0B" => "Not executable in PROGRAM mode",
            "13" => "FCS error",
            "14" => "Format error",
            "15" => "Entry number data error",
            "16" => "Command not supported",
            "18" => "Frame length error",
            "19" => "Not executable",
            "20" => "Could not create table",
            "21" => "Not executable due to CPU unit error",
            "23" => "Too much data for area",
            "A3" => "Aborted due to FCS error",
            "A4" => "Aborted due to format error",
            "A8" => "Aborted due to frame length error",
            _ => $"Unknown error code: {endCode}"
        };
    }
}

public class HostLinkResponse
{
    public bool IsValid { get; set; }
    public bool IsSuccess { get; set; }
    public int NodeAddress { get; set; }
    public string Command { get; set; } = string.Empty;
    public string EndCode { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
