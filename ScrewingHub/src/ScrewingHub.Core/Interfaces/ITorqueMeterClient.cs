using System;

namespace ScrewingHub.Core.Interfaces;

public interface ITorqueMeterClient : IDisposable
{
    event EventHandler<double>? DataReceived;
    event EventHandler<string>? ErrorOccurred;
    event EventHandler<bool>? ConnectionStatusChanged;

    bool IsConnected { get; }
    
    void Connect(string portName, int baudRate);
    void Disconnect();
}
