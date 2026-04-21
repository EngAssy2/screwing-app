namespace ScrewingHub.Communication.OmronPlc;

/// <summary>
/// Calculates the Frame Check Sequence (FCS) for Omron Host Link protocol.
/// FCS is the 8-bit XOR of all characters from '@' to just before the FCS position.
/// Result is returned as a 2-character uppercase hex string.
/// </summary>
public static class FcsCalculator
{
    /// <summary>
    /// Calculate FCS for a Host Link command frame (without the FCS, *, and CR).
    /// </summary>
    /// <param name="frame">Frame content from '@' up to (but not including) FCS position.</param>
    /// <returns>2-character hex FCS string, e.g., "4A".</returns>
    public static string Calculate(string frame)
    {
        byte fcs = 0;
        foreach (char c in frame)
        {
            fcs ^= (byte)c;
        }
        return fcs.ToString("X2");
    }

    /// <summary>
    /// Build a complete Host Link command frame with FCS, terminator, and CR.
    /// </summary>
    /// <param name="frameWithoutFcs">Frame from '@' through data (no FCS/*/CR).</param>
    /// <returns>Complete frame: {frame}{FCS}*\r</returns>
    public static string BuildCompleteFrame(string frameWithoutFcs)
    {
        var fcs = Calculate(frameWithoutFcs);
        return $"{frameWithoutFcs}{fcs}*\r";
    }
}
