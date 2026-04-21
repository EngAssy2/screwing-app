namespace ScrewingHub.Communication.OmronPlc;

/// <summary>
/// Builds Omron Host Link (C-mode) command frames.
/// Frame format: @{NodeAddr}{Command}{Data}{FCS}*\r
/// </summary>
public class HostLinkCommandBuilder
{
    private readonly int _nodeAddress;

    public HostLinkCommandBuilder(int nodeAddress = 0)
    {
        _nodeAddress = nodeAddress;
    }

    /// <summary>
    /// Build a Write DM (WD) command to write one or more words.
    /// </summary>
    /// <param name="startAddress">Starting DM address (e.g., 100).</param>
    /// <param name="values">Values to write (16-bit words).</param>
    /// <returns>Complete command frame string.</returns>
    public string BuildWriteDm(int startAddress, params int[] values)
    {
        var node = _nodeAddress.ToString("D2");
        var addr = startAddress.ToString("D4");
        var data = string.Join("", values.Select(v => v.ToString("X4")));

        var frame = $"@{node}WD{addr}{data}";
        return FcsCalculator.BuildCompleteFrame(frame);
    }

    /// <summary>
    /// Build a Read DM (RD) command to read one or more words.
    /// </summary>
    /// <param name="startAddress">Starting DM address.</param>
    /// <param name="wordCount">Number of words to read.</param>
    /// <returns>Complete command frame string.</returns>
    public string BuildReadDm(int startAddress, int wordCount)
    {
        var node = _nodeAddress.ToString("D2");
        var addr = startAddress.ToString("D4");
        var count = wordCount.ToString("D4");

        var frame = $"@{node}RD{addr}{count}";
        return FcsCalculator.BuildCompleteFrame(frame);
    }

    /// <summary>
    /// Build a Status Read (MS) command.
    /// </summary>
    /// <returns>Complete command frame string.</returns>
    public string BuildStatusRead()
    {
        var node = _nodeAddress.ToString("D2");
        var frame = $"@{node}MS";
        return FcsCalculator.BuildCompleteFrame(frame);
    }
}
