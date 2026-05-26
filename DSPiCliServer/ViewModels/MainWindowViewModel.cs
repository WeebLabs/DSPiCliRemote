using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSPiCliServer.Services;
using DSPiConsole.Usb;

namespace DSPiCliServer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private TcpServerService? _server;
    private HttpServerService? _httpServer;

    public static string Version => "1.0";
    
    public ObservableCollection<string> Logs { get; } = new();
    
    private void StartServer(int port, int httpPort)
    {
        HtmlPages.LoadPages(AppContext.BaseDirectory);
        if (string.IsNullOrEmpty(HtmlPages.GetIndexHtml("")))
        {
            // Try project root if in dev
            HtmlPages.LoadPages(Directory.GetCurrentDirectory());
        }

        _server = new TcpServerService(port);
        // .NET 8 allergy?
        // _server.OnLog += msg => 
        // {
        //     Dispatcher.UIThread.Post(() => 
        //     {
        //         Logs.Add($"[{DateTime.Now:T}] {msg}");
        //     });
        // };
        
        _server.Start();

        _httpServer = new HttpServerService(httpPort);
        // _httpServer.OnLog += msg =>
        // {
        //     Dispatcher.UIThread.Post(() =>
        //     {
        //         Logs.Add($"[{DateTime.Now:T}] [HTTP] {msg}");
        //     });
        // };
        _httpServer.Start();
        
    }

    public void StopServices()
    {
        _httpServer?.Stop();
        _httpServer = null;
        _server?.Stop();
        _server = null;
        DeviceManager.Instance.Stop();
    }

    public void OnErrorMessage(DeviceManager devService, object? sender, EventArgs e)
    {
       Console.WriteLine(devService.MyDevice.ErrorMessage);
    }

    private void OnConnected(DeviceManager devService, object? sender, EventArgs e)
    {
        if (devService.IsConnected)
        {
            var info = devService.MyDevice.GetDeviceInfo();
            var newPlatform = info?.Platform ?? "";
            // Set channel count for platform-aware status parsing
            devService.MyDevice.NumChannels = newPlatform == "RP2350" ? 11 : 7;
            var bulk = devService.MyDevice.GetAllParams();
            if (bulk != null)
            {
                var parsed = BulkParamsParser.Parse(bulk);
                if (parsed != null)
                {
                    var infos = $"Loudness={parsed.LoudnessEnabled}, PreampGain={parsed.PreampGainDb}";
                    TcpServerService.Server?.WriteClientAsync(infos).Wait();
                    return;
                }
            }
        }
        else
        {
            // Keep Platform so the UI layout stays until a new device connects
            // ResetChannelData();
            // _presetsChecked = false;
            // ActivePreset = -1;
            // PresetsDirty = false;
        }
    }

    private void SetOnProperty(DeviceManager devService, Dictionary<string, Func<string>> dict)
    {
        // Subscribe to device events
        devService.MyDevice.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DspDevice.IsConnected))
            {
                OnConnected(devService, s, e);
            }
            else if (e.PropertyName == nameof(DspDevice.ErrorMessage))
            {
                OnErrorMessage(devService, s, e);
            }
            else if (e.PropertyName == nameof(DspDevice.SelectedDeviceInfo))
            {

            }
        };
    }


    private void SetupDevice()
    {
        DeviceManager.Instance.Start();
        SetOnProperty(DeviceManager.Instance, new());
    }

    public MainWindowViewModel(int port = 8082, int httpPort = 8080)
    {
        StartServer(port, httpPort);
        SetupDevice();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
    }
}
