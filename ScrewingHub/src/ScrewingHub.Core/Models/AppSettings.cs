namespace ScrewingHub.Core.Models;

/// <summary>
/// Application settings persisted to appsettings.json.
/// </summary>
public class AppSettings
{
    // DTM10 serial settings
    public SerialSettings Dtm10Serial { get; set; } = new()
    {
        PortName = "COM3",
        BaudRate = 38400,
        DataBits = 8,
        Parity = "None",
        StopBits = "One",
        Handshake = "None"
    };

    // Torque Meter serial settings
    public SerialSettings TorqueMeterSerial { get; set; } = new()
    {
        PortName = "COM5",
        BaudRate = 9600,
        DataBits = 8,
        Parity = "None",
        StopBits = "One",
        Handshake = "None"
    };

    // PLC serial settings
    public PlcSerialSettings PlcSerial { get; set; } = new()
    {
        PortName = "COM4",
        BaudRate = 9600,
        DataBits = 7,
        Parity = "Even",
        StopBits = "Two",
        Handshake = "None",
        NodeAddress = 0,
        DmStartAddress = 100,
        HeartbeatIntervalMs = 200,
        CommandTimeoutMs = 1000
    };

    // Data logging
    public string CsvLogFolder { get; set; } = @"D:\ScrewingHub\Logs";
    public string CsvFileNamePattern { get; set; } = "[Status]_[ModelNumber]_[Date]_[StationName].csv";

    // Product models
    public List<ProductModel> Models { get; set; } = new();

    // General
    public string DefaultOperatorId { get; set; } = string.Empty;
    public string NgSoundFilePath { get; set; } = string.Empty;
    public bool AutoStartOnBoot { get; set; } = false;
    public int ReconnectIntervalMs { get; set; } = 3000;
}

public class SerialSettings
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
}

public class PlcSerialSettings : SerialSettings
{
    public int NodeAddress { get; set; } = 0;
    public int DmStartAddress { get; set; } = 100;
    public int HeartbeatIntervalMs { get; set; } = 200;
    public int PollingIntervalMs { get; set; } = 500;
    public int CommandTimeoutMs { get; set; } = 1000;
}
