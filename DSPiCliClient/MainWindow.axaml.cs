using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Input;

namespace DSPiCliClient;

public partial class MainWindow : Window
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    
    private List<string> CommandList { get; set; } = new List<string>();
    private int CurrentCommand { get; set; } = -1;
    private readonly int _maxCommandCount = 10;

    public MainWindow()
    {
        InitializeComponent();
        
        AddressBox.PropertyChanged += (s, e) => { if (e.Property.Name == "Text") Disconnect(); };
        PortBox.PropertyChanged += (s, e) => { if (e.Property.Name == "Text") Disconnect(); };

        // Handle KeyDown
        InputBox.KeyDown += InputBox_KeyDown;
    }

    private void Disconnect()
    {
        _writer?.Dispose(); _writer = null;
        _reader?.Dispose(); _reader = null;
        _stream?.Dispose(); _stream = null;
        _client?.Dispose(); _client = null;
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        Disconnect();
        await ConnectToServer();
    }

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            //SendCommand(InputBox.Text);
            var cnt = CurrentCommand;
            if (cnt == -1 || cnt >= CommandList.Count)
                cnt = CommandList.Count-1;
            if (cnt >= 0 && cnt < CommandList.Count)
            {
                InputBox.Text = CommandList[cnt];
                CurrentCommand = cnt - 1;
                if (CurrentCommand < 0)
                    CurrentCommand = 0;
            }
        }
        else if (e.Key == Key.Down && CurrentCommand != -1)
        {
            var cnt = CurrentCommand;
            if (cnt >= CommandList.Count)
                cnt = CommandList.Count - 1;
            if (cnt >= 0)
            {
                cnt++;
                if (cnt == CommandList.Count)
                {
                    InputBox.Text = string.Empty;
                }
                else
                {
                    CurrentCommand = cnt;
                    InputBox.Text = CommandList[CurrentCommand];
                }
            }
        }
    }

    private async Task ConnectToServer()
    {
        var address = AddressBox.Text ?? "localhost";
        if (!int.TryParse(PortBox.Text, out int port)) port = 8082;

        Log($"Connecting to {address}:{port}...");
        
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(address, port);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
            Log("Connected to server.");
        }
        catch (Exception ex)
        {
            Log($"Connection failed: {ex.Message}");
            _client?.Dispose();
            _client = null;
        }
    }

    private void Log(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            DisplayBlock.Text += $"[{DateTime.Now:T}] {message}\n";
        });
    }

    private void OnOpenDeviceControlClick(object sender, RoutedEventArgs e)
    {
        var address = AddressBox.Text ?? "localhost";
        if (!int.TryParse(PortBox.Text, out int port)) port = 8082;
        
        var win = new DeviceControlWindow(address, port);
        win.Show();
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (_writer == null)
        {
            await ConnectToServer();
        }

        if (_writer == null)
        {
            Log("Not connected to server.");
            return;
        }

        try
        {
            Log($"Sending: {text}");
            await _writer.WriteLineAsync(text);
            InputBox.Text = string.Empty;
            CommandList.Add(text);
            if (CommandList.Count > _maxCommandCount)
            {
                CommandList.RemoveAt(0);
            }
            CurrentCommand = -1;
            var response = await _reader!.ReadLineAsync();
            Log($"Received: {response ?? "null"}");
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
        base.OnClosed(e);
    }
}
