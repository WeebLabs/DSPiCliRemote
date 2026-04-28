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

    private readonly List<TcpClient> _clients = new();
    private readonly object _clientsLock = new();

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
        
        lock (_clientsLock)
        {
            foreach (var client in _clients)
            {
                try { client.Dispose(); } catch { /* Ignore */ }
            }
            _clients.Clear();
        }

        Console.WriteLine("Server stopped.");
        OnLog?.Invoke("Server stopped.");
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                
                // Set timeouts to prevent hanging on unreliable networks
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                client.NoDelay = true; // Send data immediately

                lock (_clientsLock)
                {
                    _clients.Add(client);
                }

                _ = Task.Run(async () => 
                {
                    try
                    {
                        await HandleClientAsync(client, ct);
                    }
                    finally
                    {
                        lock (_clientsLock)
                        {
                            _clients.Remove(client);
                        }
                    }
                }, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    OnLog?.Invoke($"Accept error: {ex.Message}");
                    // Wait a bit before retrying to avoid tight loop on persistent error
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
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            var remoteEndPoint = client.Client.RemoteEndPoint;
            OnLog?.Invoke($"Client connected: {remoteEndPoint}");

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(ct);
                    if (line == null) 
                        break;

                    OnLog?.Invoke($"[{remoteEndPoint}] Received: {line}");

                    string response = CommandParser.ProcessCommandPublic(line);
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
        TcpClient[] clientsToNotify;
        lock (_clientsLock)
        {
            clientsToNotify = _clients.ToArray();
        }

        var tasks = clientsToNotify.Select(async client =>
        {
            try
            {
                var stream = client.GetStream();
                var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                await writer.WriteLineAsync(message);
                OnLog?.Invoke($"[Broadcast] Sent to {client.Client.RemoteEndPoint}: {message}");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Broadcast] Error sending to {client.Client.RemoteEndPoint}: {ex.Message}");
                // We don't remove the client here, HandleClientAsync's finally block will do it if the connection is dead
            }
        });

        await Task.WhenAll(tasks);
    }
   
}
