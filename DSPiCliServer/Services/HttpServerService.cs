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
                if (method == "OPTIONS")
                {
                    await SendOptionsResponse(stream);
                    return;
                }

                OnLog?.Invoke($"HTTP {method} {path}");
                if (path == "/api/command" && method == "POST")
                {
                    await HandleApiCommand(stream, headers, reader, ct);
                }
                else if ((path == "/" || path == "/index.html") && method == "GET")
                {
                    await ServeIndexHtml(stream);
                }
                else if (path == "/cli" && method == "GET")
                {
                    await ServeCliHtml(stream);
                }
                else if (path == "/js/script.js" && method == "GET")
                {
                    await ServeStaticFile(stream, "wwwroot/js/script.js", "text/javascript");
                }
                else if (path == "/js/cli.js" && method == "GET")
                {
                    await ServeStaticFile(stream, "wwwroot/js/cli.js", "text/javascript");
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

    private async Task SendOptionsResponse(Stream stream)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("HTTP/1.1 204 No Content\r\n");
        sb.Append("Access-Control-Allow-Origin: *\r\n");
        sb.Append("Access-Control-Allow-Methods: POST, GET, OPTIONS\r\n");
        sb.Append("Access-Control-Allow-Headers: Content-Type\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append("\r\n");
        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await stream.WriteAsync(bytes, 0, bytes.Length);
        await stream.FlushAsync();
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
        string html = HtmlPages.GetIndexHtml(_localIp);
        await SendResponseAsync(stream, 200, "OK", "text/html", html);
    }

    private async Task ServeCliHtml(Stream stream)
    {
        string html = HtmlPages.GetCliHtml();
        await SendResponseAsync(stream, 200, "OK", "text/html", html);
    }

    private async Task ServeStaticFile(Stream stream, string relativePath, string contentType)
    {
        try
        {
            // Normalize path for the current OS
            string normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            
            string fullPath = Path.Combine(AppContext.BaseDirectory, normalizedPath);
            if (!File.Exists(fullPath))
            {
                // Try project root if in dev
                fullPath = Path.Combine(Directory.GetCurrentDirectory(), normalizedPath);
                if (!File.Exists(fullPath))
                {
                    // Try one more level up or explicit folder
                    fullPath = Path.Combine(Directory.GetCurrentDirectory(), "DSPiCliServer", normalizedPath);
                }
            }

            if (File.Exists(fullPath))
            {
                OnLog?.Invoke($"Serving file: {fullPath}");
                string content = await File.ReadAllTextAsync(fullPath);
                await SendResponseAsync(stream, 200, "OK", contentType, content);
            }
            else
            {
                OnLog?.Invoke($"File not found: {relativePath} (Searched in {AppContext.BaseDirectory} and {Directory.GetCurrentDirectory()})");
                await SendResponseAsync(stream, 404, "Not Found", "text/plain", $"File not found: {relativePath}");
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Error serving static file: {ex.Message}");
            await SendResponseAsync(stream, 500, "Internal Server Error", "text/plain", ex.Message);
        }
    }

    private async Task SendResponseAsync(Stream stream, int statusCode, string statusText, string contentType, string content)
    {
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        StringBuilder sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {statusCode} {statusText}\r\n");
        sb.Append($"Content-Type: {contentType}\r\n");
        sb.Append($"Content-Length: {contentBytes.Length}\r\n");
        sb.Append("Access-Control-Allow-Origin: *\r\n");
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

}
