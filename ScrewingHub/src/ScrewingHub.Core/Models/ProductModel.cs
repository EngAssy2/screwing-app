namespace ScrewingHub.Core.Models;

/// <summary>
/// Configuration for a product model (e.g., Model A or Model B).
/// Each model has its own screw count and threshold specifications.
/// </summary>
public class ProductModel
{
    public int ModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public int TotalScrewCount { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Channel configurations for this model.</summary>
    public List<ChannelConfig> Channels { get; set; } = new();

    /// <summary>Get the channel config for a specific DTM10 channel number.</summary>
    public ChannelConfig? GetChannel(int channelNumber)
        => Channels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
}
