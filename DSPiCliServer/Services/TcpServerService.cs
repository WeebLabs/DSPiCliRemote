using System.Net;
using System.Net.Sockets;
using System.Text;
using DSPiConsole.Usb;

namespace DSPiCliServer.Services;

public class TcpServerService
{
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    public static TcpServerService? Server { get; private set; }

    public event Action<string>? OnLog;

    public TcpServerService(int port = 8082)
    {
        _port = port;
        Server = this;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        OnLog?.Invoke($"Server started on port {_port}...");
        Console.WriteLine($"Server started on port {_port}...");

        Task.Run(() => AcceptClientsAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        Console.WriteLine("Server stopped.");
        OnLog?.Invoke("Server stopped.");
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Accept error: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            var remoteEndPoint = client.Client.RemoteEndPoint;
            OnLog?.Invoke($"Client connected: {remoteEndPoint}");

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    OnLog?.Invoke($"[{remoteEndPoint}] Received: {line}");

                    string response = ProcessCommand(line);
                    Console.WriteLine($"{line}=>{response}");
                    
                    await writer.WriteLineAsync(response);
                    OnLog?.Invoke($"[{remoteEndPoint}] Sent: {response}");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[{remoteEndPoint}] Error: {ex.Message}");
                Console.WriteLine($"[{remoteEndPoint}] Error: {ex.Message}");
            }
            finally
            {
                OnLog?.Invoke($"[{remoteEndPoint}] Client disconnected.");
            }
        }
    }

    public async Task WriteClientAsync(string message)
    {
        var client = await _listener!.AcceptTcpClientAsync();            
        using (var stream = client.GetStream())
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            var remoteEndPoint = client.Client.RemoteEndPoint;
            OnLog?.Invoke($"Client connected: {remoteEndPoint}");

            try
            {
                //while (!ct.IsCancellationRequested)
                {
                    await writer.WriteLineAsync(message);
                    OnLog?.Invoke($"[{remoteEndPoint}] Sent: {message}");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[{remoteEndPoint}] Error: {ex.Message}");
            }
            finally
            {
                OnLog?.Invoke($"[{remoteEndPoint}] Client disconnected.");
            }
        }
    }

    private string DoHello()
    {
        var dv = DeviceManager.Instance;
        if (dv.IsConnected)
        {
            var bks = dv.MyDevice.GetAllParams();
            if(bks == null) 
                return "Not connected";
            var parsed = BulkParamsParser.Parse(bks);
            if (parsed != null)
            {
                //MyBulkParams = parsed;
                var infos = $"Loudness={parsed.LoudnessEnabled}, PreampGain={parsed.PreampGainDb}";
                return infos;
            }
        }
        return "Not connected";
    }
    
    
    public string ProcessCommandPublic(string input) => ProcessCommand(input);

    private string ProcessCommand(string input)
    {
        // Simple echo/interpreter logic
        if (string.IsNullOrWhiteSpace(input)) return "Error: Empty command";
        
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var dv = DeviceManager.Instance;

        try
        {
            return command switch
            {
                "hello" => DoHello(),
                "ping" => "pong",
                "time" => DateTime.Now.ToString("T"),
                "date" => DateTime.Now.ToString("d"),
                "help" => "Available commands: ping, time, date, help, hello, get_vol, set_vol <db>, get_bypass, set_bypass <0/1>, get_loudness, set_loudness <0/1>, get_leveling, set_leveling <0/1>, get_crossfeed, set_crossfeed <0/1>, get_samplerate, get_deviceid",
                "get_vol" => dv.IsConnected ? (dv.MyDevice.GetMasterVolume()?.ToString("F1") ?? "Error") : "Not connected",
                "set_vol" => (dv.IsConnected && parts.Length > 1 && float.TryParse(parts[1], out float vol)) ? (dv.MyDevice.SetMasterVolume(vol) ? "OK" : "Error") : "Error",
                "get_bypass" => dv.IsConnected ? (dv.MyDevice.GetBypass()?.ToString() ?? "Error") : "Not connected",
                "set_bypass" => (dv.IsConnected && parts.Length > 1) ? (dv.MyDevice.SetBypass(parts[1] == "1") ? "OK" : "Error") : "Error",
                "get_loudness" => dv.IsConnected ? (dv.MyDevice.GetLoudnessEnabled()?.ToString() ?? "Error") : "Not connected",
                "set_loudness" => (dv.IsConnected && parts.Length > 1) ? (dv.MyDevice.SetLoudnessEnabled(parts[1] == "1") ? "OK" : "Error") : "Error",
                "get_leveling" => dv.IsConnected ? (dv.MyDevice.GetLevellerEnabled()?.ToString() ?? "Error") : "Not connected",
                "set_leveling" => (dv.IsConnected && parts.Length > 1) ? (dv.MyDevice.SetLevellerEnabled(parts[1] == "1") ? "OK" : "Error") : "Error",
                "get_crossfeed" => dv.IsConnected ? (dv.MyDevice.GetCrossfeedEnabled()?.ToString() ?? "Error") : "Not connected",
                "set_crossfeed" => (dv.IsConnected && parts.Length > 1) ? (dv.MyDevice.SetCrossfeedEnabled(parts[1] == "1") ? "OK" : "Error") : "Error",
                "get_samplerate" => dv.IsConnected ? (dv.MyDevice.GetStatusUInt32(15)?.ToString() ?? "Error") : "Not connected", 
                "get_deviceid" => dv.IsConnected ? (dv.MyDevice.GetDeviceSerial() ?? "Unknown") : "Not connected",
                "get_activepreset" => dv.IsConnected ? dv.MyDevice.GetActivePreset().ToString() : "Not connected",
                "get_presets" => GetPresetsCommand(dv),
                "set_preset" => (dv.IsConnected && parts.Length > 1 && int.TryParse(parts[1], out int slot)) ? (dv.MyDevice.LoadPreset(slot) == 0 ? "OK" : "Error") : "Error",
                _ => $"Echo: {input}"
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private string GetPresetsCommand(DeviceManager dv)
    {
        if (!dv.IsConnected) return "Not connected";
        var dir = dv.MyDevice.GetPresetDirectory();
        if (dir == null) return "Error";

        var active = dv.MyDevice.GetActivePreset();
        var sb = new StringBuilder();
        sb.Append(active).Append('|');

        bool first = true;
        for (int i = 0; i < 16; i++)
        {
            if ((dir.Value.OccupiedMask & (1 << i)) != 0)
            {
                if (!first) sb.Append(',');
                var name = dv.MyDevice.GetPresetName(i) ?? $"Preset {i}";
                sb.Append(i).Append(':').Append(name);
                first = false;
            }
        }
        return sb.ToString();
    }
}
