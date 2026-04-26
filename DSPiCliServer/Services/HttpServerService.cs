using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DSPiCliServer.Services;
using DSPiConsole.Usb;

namespace DSPiCliServer.Services;

public class HttpServerService
{
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly TcpServerService _tcpService;
    private string _localIp = "Unknown";

    public event Action<string>? OnLog;

    public HttpServerService(TcpServerService tcpService, int port = 80)
    {
        _tcpService = tcpService;
        _port = port;
    }

    private void StartServer()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        
        try
        {
            _listener.Start();
            OnLog?.Invoke($"HTTP Server (TcpListener) started on port {_port}...");
            Console.WriteLine($"HTTP Server (TcpListener) started on port {_port}...");
            Task.Run(() => ListenAsync(_cts!.Token));
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"HTTP Server failed to start: {ex.Message}");
            Console.WriteLine($"HTTP Server failed to start: {ex.Message}");
        }
        // just do this once per server instance
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    _localIp = ip.ToString();
                    break;
                }
            }
        }
        catch { /* Ignore */ }    
    }
    

    public void Start()
    {
        _cts = new CancellationTokenSource();
        StartServer();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        OnLog?.Invoke("HTTP Server stopped.");
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // whenever we get a new client connection
                // we create a new task to handle it in the background
                var client = await _listener!.AcceptTcpClientAsync(ct);
                
                // Set timeouts for HTTP requests
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    OnLog?.Invoke($"HTTP Error: {ex.Message}");
                    // Wait a bit before retrying
                    await Task.Delay(1000, ct);
                }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            try
            {
                // 1. Read Request Line
                // Use a cancellation token with a timeout for initial request line
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(TimeSpan.FromSeconds(5));

                string? requestLine = await reader.ReadLineAsync(readCts.Token);
                if (string.IsNullOrEmpty(requestLine)) 
                    return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 3) 
                    return;

                string method = parts[0];
                string path = parts[1];
                string version = parts[2];

                // 2. Read Headers
                var headers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? headerLine;
                while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync(readCts.Token)))
                {
                    int colonIndex = headerLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        string key = headerLine.Substring(0, colonIndex).Trim();
                        string value = headerLine.Substring(colonIndex + 1).Trim();
                        headers[key] = value;
                    }
                }

                // 3. Route request
                //Console.WriteLine($"HTTP Request: {method} {path}");
                if (path == "/api/command" && method == "POST")
                {
                    await HandleApiCommand(stream, headers, reader, ct);
                }
                else if ((path == "/" || path == "/index.html") && method == "GET")
                {
                    await ServeIndexHtml(stream);
                }
                else
                {
                    await SendResponseAsync(stream, 404, "Not Found", "text/plain", "Not Found");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"HandleClient Error: {ex.Message}");
                try
                {
                    if(stream.CanWrite && ex is not OperationCanceledException)
                        await SendResponseAsync(stream, 500, "Internal Server Error", "text/plain", "Internal Server Error");
                }
                catch { /* Ignore */ }
            }
        }
    }

    private async Task HandleApiCommand(Stream stream, System.Collections.Generic.Dictionary<string, string> headers, StreamReader reader, CancellationToken ct)
    {
        string body = "";
        if (headers.TryGetValue("Content-Length", out string? lengthStr) && int.TryParse(lengthStr, out int length))
        {
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readCts.CancelAfter(TimeSpan.FromSeconds(5));

            char[] buffer = new char[length];
            int read = 0;
            while (read < length)
            {
                int n = await reader.ReadAsync(buffer.AsMemory(read, length - read), readCts.Token);
                if (n == 0) 
                    break;
                read += n;
            }
            body = new string(buffer, 0, read);
        }

        OnLog?.Invoke($"HTTP API Received: {body}");

        string result = InvokeTcpProcessCommand(body);
        Console.WriteLine($"HTTP API Flow: {body}=>{result}");
        await SendResponseAsync(stream, 200, "OK", "text/plain", result);
    }

    private async Task ServeIndexHtml(Stream stream)
    {
        string html = GetIndexHtml(_localIp);
        await SendResponseAsync(stream, 200, "OK", "text/html", html);
    }

    private async Task SendResponseAsync(Stream stream, int statusCode, string statusText, string contentType, string content)
    {
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        StringBuilder sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {statusCode} {statusText}\r\n");
        sb.Append($"Content-Type: {contentType}\r\n");
        sb.Append($"Content-Length: {contentBytes.Length}\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append("\r\n");

        byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
        await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
        await stream.FlushAsync();
    }

    private string InvokeTcpProcessCommand(string command)
    {
        return _tcpService.ProcessCommandPublic(command);
    }

    private string GetIndexHtml(string ipAddress)
    {
        return @$"<!DOCTYPE html>
<html>
<head>
    <title>DSPi Web Control</title>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <style>
        body {{ font-family: sans-serif; padding: 20px; max-width: 600px; margin: auto; background: #f0f0f0; }}
        .card {{ background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 20px; }}
        .control-group {{ margin-bottom: 15px; }}
        .btn-row {{ display: flex; gap: 10px; margin-bottom: 15px; flex-wrap: wrap; }}
        label {{ display: block; margin-bottom: 5px; font-weight: bold; }}
        input[type=range] {{ width: 100%; }}
        button {{ padding: 10px 20px; cursor: pointer; background: #007bff; color: white; border: none; border-radius: 4px; }}
        button:hover {{ background: #0056b3; }}
        .status {{ font-size: 0.9em; color: #666; margin-top: 10px; }}
        #log {{ height: 100px; overflow-y: scroll; background: #eee; padding: 10px; font-family: monospace; font-size: 0.8em; }}
    </style>
</head>
<body>
    <div class='card'>
        <h2>DSPi Web Control</h2>
        <div class='control-group'>
            <label>Master Volume: <span id='volLabel'>0.0</span> dB</label>
            <input type='range' id='volSlider' min='-100' max='12' step='0.5' value='0'>
        </div>
        <div class='btn-row'>
            <button id='loudnessBtn'>Loudness: OFF</button>
            <button id='levelingBtn'>Leveling: OFF</button>
            <button id='crossfeedBtn'>Crossfeed: OFF</button>
        </div>
        <div class='control-group'>
            <label for='presetSelect'>Preset:</label>
            <select id='presetSelect' style='width: 100%; padding: 8px; border-radius: 4px; border: 1px solid #ccc;'>
                <option value='-1'>Loading...</option>
            </select>
        </div>
        <div class='control-group'>
            <button id='refreshBtn'>Refresh Status</button>
        </div>
        <div class='status'>
            <div>Sample Rate: <span id='srText'>-</span></div>
            <div>Device ID: <span id='idText'>-</span></div>
            <div style='margin-top: 10px; font-weight: bold;'>Server IP: {ipAddress}</div>
        </div>
    </div>
    <div class='card'>
        <h3>Log</h3>
        <div id='log'></div>
    </div>

    <script>
        const volSlider = document.getElementById('volSlider');
        const volLabel = document.getElementById('volLabel');
        const loudnessBtn = document.getElementById('loudnessBtn');
        const levelingBtn = document.getElementById('levelingBtn');
        const crossfeedBtn = document.getElementById('crossfeedBtn');
        const presetSelect = document.getElementById('presetSelect');
        const refreshBtn = document.getElementById('refreshBtn');
        const logDiv = document.getElementById('log');

        async function sendCommand(cmd) {{
            try {{
                const response = await fetch('/api/command', {{
                    method: 'POST',
                    body: cmd
                }});
                const text = await response.text();
                addLog(`Sent: ${{cmd}} | Received: ${{text}}`);
                return text;
            }} catch (err) {{
                addLog(`Error: ${{err}}`);
                return null;
            }}
        }}

        function addLog(msg) {{
            const entry = document.createElement('div');
            entry.textContent = `[${{new Date().toLocaleTimeString()}}] ${{msg}}`;
            logDiv.appendChild(entry);
            logDiv.scrollTop = logDiv.scrollHeight;
        }}

        volSlider.oninput = () => volLabel.textContent = volSlider.value;
        volSlider.onchange = async () => {{
            await sendCommand(`set_vol ${{volSlider.value}}`);
        }};

        let isLoudness = false;
        loudnessBtn.onclick = async () => {{
            isLoudness = !isLoudness;
            const res = await sendCommand(`set_loudness ${{isLoudness ? 1 : 0}}`);
            if (res === 'OK') {{
                loudnessBtn.textContent = `Loudness: ${{isLoudness ? 'ON' : 'OFF'}}`;
            }} else {{
                isLoudness = !isLoudness;
            }}
        }};

        let isLeveling = false;
        levelingBtn.onclick = async () => {{
            isLeveling = !isLeveling;
            const res = await sendCommand(`set_leveling ${{isLeveling ? 1 : 0}}`);
            if (res === 'OK') {{
                levelingBtn.textContent = `Leveling: ${{isLeveling ? 'ON' : 'OFF'}}`;
            }} else {{
                isLeveling = !isLeveling;
            }}
        }};

        let isCrossfeed = false;
        crossfeedBtn.onclick = async () => {{
            isCrossfeed = !isCrossfeed;
            const res = await sendCommand(`set_crossfeed ${{isCrossfeed ? 1 : 0}}`);
            if (res === 'OK') {{
                crossfeedBtn.textContent = `Crossfeed: ${{isCrossfeed ? 'ON' : 'OFF'}}`;
            }} else {{
                isCrossfeed = !isCrossfeed;
            }}
        }};

        presetSelect.onchange = async () => {{
            const slot = presetSelect.value;
            if (slot === '-1') return;
            const res = await sendCommand(`set_preset ${{slot}}`);
            if (res !== 'OK') {{
                addLog('Failed to set preset');
                await refreshPresets();
            }}
        }};

        async function refreshPresets() {{
            const presetsStr = await sendCommand('get_presets');
            if (!presetsStr || presetsStr === 'Error' || presetsStr === 'Not connected') {{
                presetSelect.innerHTML = '<option value=""-1"">No Presets Found</option>';
                return;
            }}

            const resParts = presetsStr.split('|');
            const activeSlot = resParts[0];
            const listStr = resParts[1];
            presetSelect.innerHTML = '';
            
            if (listStr) {{
                const items = listStr.split(',');
                for (const item of items) {{
                    const itemParts = item.split(':');
                    const slot = itemParts[0];
                    const name = itemParts[1];
                    const opt = document.createElement('option');
                    opt.value = slot;
                    opt.textContent = name;
                    if (slot === activeSlot) opt.selected = true;
                    presetSelect.appendChild(opt);
                }}
            }} else {{
                presetSelect.innerHTML = '<option value=""-1"">No Presets Found</option>';
            }}
        }}

        async function refresh() {{
            await refreshPresets();

            const activePreset = await sendCommand('get_activepreset');
            if (activePreset && !isNaN(parseInt(activePreset))) {{
                presetSelect.value = activePreset;
            }}

            const vol = await sendCommand('get_vol');
            if (!isNaN(parseFloat(vol))) {{
                volSlider.value = vol;
                volLabel.textContent = vol;
            }}

            const loudness = await sendCommand('get_loudness');
            isLoudness = loudness.toLowerCase() === 'true';
            loudnessBtn.textContent = `Loudness: ${{isLoudness ? 'ON' : 'OFF'}}`;

            const leveling = await sendCommand('get_leveling');
            isLeveling = leveling.toLowerCase() === 'true';
            levelingBtn.textContent = `Leveling: ${{isLeveling ? 'ON' : 'OFF'}}`;

            const crossfeed = await sendCommand('get_crossfeed');
            isCrossfeed = crossfeed.toLowerCase() === 'true';
            crossfeedBtn.textContent = `Crossfeed: ${{isCrossfeed ? 'ON' : 'OFF'}}`;

            document.getElementById('srText').textContent = await sendCommand('get_samplerate') + ' Hz';
            document.getElementById('idText').textContent = await sendCommand('get_deviceid');
        }}

        refreshBtn.onclick = refresh;
        window.onload = refresh;
    </script>
</body>
</html>";
    }
}
