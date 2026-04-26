using DSPiConsole.Usb;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DSPiCliServer.Services;

public partial class DeviceManager : ObservableObject
{
    public static DeviceManager Instance { get; } = new();
    
    private readonly DspDevice _device;
    private CancellationTokenSource? _monitorCts;
    
    [ObservableProperty]
    private bool _isConnected;

    public DspDevice MyDevice => _device;

    public DeviceManager()
    {
        _device = new DspDevice();
        // Forward the connected status from DspDevice
        _device.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DspDevice.IsConnected))
            {
                IsConnected = _device.IsConnected;
            }
        };
    }

    public void Start()
    {
        Stop();
        _monitorCts = new CancellationTokenSource();
        _device.StopMonitoring();
        _device.StartMonitoring();  // start looking for devices
    }

    public void Stop()
    {
        _device.StopMonitoring();
        
        _monitorCts?.Cancel();
    }
}
