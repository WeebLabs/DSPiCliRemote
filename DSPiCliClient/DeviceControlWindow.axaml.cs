using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DSPiCliClient;

public partial class DeviceControlWindow : Window
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    private bool _isLoudnessEnabled;
    private bool _isLevelingEnabled;
    private bool _isCrossfeedEnabled;
    private bool _isInitialLoading;
    
    public DeviceViewModel DevModel { get; } = new();

    private string _address = "localhost";
    private int _port = 8082;

    public DeviceControlWindow()
    {
        InitializeComponent();
        DataContext = DevModel;

        VolumeText.Text = VolumeSlider.Value.ToString("F1");
        
        VolumeSlider.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "Value")
            {
                var val = (double)e.NewValue!;
                VolumeText.Text = val.ToString("F1");
                if (!_isInitialLoading)
                {
                    _ = SendCommandAsync($"set_vol {val:F1}");
                }
            }
        };
    }

    public DeviceControlWindow(string address, int port) : this()
    {
        _address = address;
        _port = port;
        _ = ConnectAndInitAsync();
    }

    private async Task ConnectAndInitAsync()
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_address, _port);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };

            NetworkAddrText.Text = _client.Client.RemoteEndPoint?.ToString() ?? $"{_address}:{_port}";
            
            await RefreshAllStatusAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error connecting: {ex.Message}");
        }
    }

    private async Task RefreshAllStatusAsync()
    {
        if (_writer == null || _reader == null) return;

        _isInitialLoading = true;
        try
        {
            // Volume
            var volStr = await SendCommandAsync("get_vol");
            if (float.TryParse(volStr, out float vol))
            {
                VolumeSlider.Value = vol;
                VolumeText.Text = vol.ToString("F1");
            }

            // Loudness
            var loudnessStr = await SendCommandAsync("get_loudness");
            _isLoudnessEnabled = loudnessStr?.ToLower() == "true";
            UpdateLoudnessButton();

            // Leveling
            var levelingStr = await SendCommandAsync("get_leveling");
            _isLevelingEnabled = levelingStr?.ToLower() == "true";
            UpdateLevelingButton();

            // Crossfeed
            var crossfeedStr = await SendCommandAsync("get_crossfeed");
            _isCrossfeedEnabled = crossfeedStr?.ToLower() == "true";
            UpdateCrossfeedButton();

            // Presets
            await RefreshPresetsAsync();

            // Sample Rate
            var srStr = await SendCommandAsync("get_samplerate");
            SampleRateText.Text = $"{srStr} Hz";

            // Device ID
            var idStr = await SendCommandAsync("get_deviceid");
            DeviceIdText.Text = idStr ?? "Unknown";
        }
        finally
        {
            _isInitialLoading = false;
        }
    }

    private async Task<string?> SendCommandAsync(string cmd)
    {
        if (_writer == null || _reader == null) return null;
        try
        {
            await _writer.WriteLineAsync(cmd);
            return await _reader.ReadLineAsync();
        }
        catch
        {
            return null;
        }
    }

    private async void OnLoudnessClick(object? sender, RoutedEventArgs e)
    {
        _isLoudnessEnabled = !_isLoudnessEnabled;
        var rslt = await SendCommandAsync($"set_loudness {(_isLoudnessEnabled ? "1" : "0")}");
        if (rslt == "OK")
        {
            UpdateLoudnessButton();
        }
        else
        {
            _isLoudnessEnabled = !_isLoudnessEnabled;
        }
    }

    private void UpdateLoudnessButton()
    {
        LoudnessButton.Content = $"Loudness: {(_isLoudnessEnabled ? "ON" : "OFF")}";
    }

    private async void OnLevelingClick(object? sender, RoutedEventArgs e)
    {
        _isLevelingEnabled = !_isLevelingEnabled;
        var rslt = await SendCommandAsync($"set_leveling {(_isLevelingEnabled ? "1" : "0")}");
        if (rslt == "OK")
        {
            UpdateLevelingButton();
        }
        else
        {
            _isLevelingEnabled = !_isLevelingEnabled;
        }
    }

    private void UpdateLevelingButton()
    {
        LevelingButton.Content = $"Leveling: {(_isLevelingEnabled ? "ON" : "OFF")}";
    }

    private async void OnCrossfeedClick(object? sender, RoutedEventArgs e)
    {
        _isCrossfeedEnabled = !_isCrossfeedEnabled;
        var rslt = await SendCommandAsync($"set_crossfeed {(_isCrossfeedEnabled ? "1" : "0")}");
        if (rslt == "OK")
        {
            UpdateCrossfeedButton();
        }
        else
        {
            _isCrossfeedEnabled = !_isCrossfeedEnabled;
        }
    }

    private void UpdateCrossfeedButton()
    {
        CrossfeedButton.Content = $"Crossfeed: {(_isCrossfeedEnabled ? "ON" : "OFF")}";
    }

    private async Task RefreshPresetsAsync()
    {
        var presetsStr = await SendCommandAsync("get_presets");
        if (string.IsNullOrEmpty(presetsStr) || presetsStr == "Error" || presetsStr == "Not connected")
        {
            DevModel.PresetList = new List<DeviceViewModel.PresetItem> 
            { 
                new DeviceViewModel.PresetItem { Name = "No Presets Found", Slot = "-1" } 
            };
            DevModel.SelectedPreset = DevModel.PresetList[0];
            return;
        }

        var resParts = presetsStr.Split('|');
        if (resParts.Length < 2) return;

        var activeSlotStr = resParts[0];
        var listStr = resParts[1];

        var items = new List<DeviceViewModel.PresetItem>();
        DeviceViewModel.PresetItem? selectedItem = null;

        if (!string.IsNullOrEmpty(listStr))
        {
            var parts = listStr.Split(',');
            foreach (var part in parts)
            {
                var itemParts = part.Split(':');
                if (itemParts.Length < 2) continue;

                var slot = itemParts[0];
                var name = itemParts[1];
                var presetItem = new DeviceViewModel.PresetItem { Name = name, Slot = slot };
                items.Add(presetItem);

                if (slot == activeSlotStr)
                {
                    selectedItem = presetItem;
                }
            }
        }

        _isInitialLoading = true; // Prevent triggering OnPresetSelectionChanged during update
        try
        {
            DevModel.PresetList = items;
            DevModel.SelectedPreset = selectedItem;
        }
        finally
        {
            _isInitialLoading = false;
        }
    }

    private async void OnPresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitialLoading) return;
        if (DevModel.SelectedPreset is { } item && item.Slot != "-1")
        {
            var res = await SendCommandAsync($"set_preset {item.Slot}");
            if (res != "OK")
            {
                await RefreshPresetsAsync();
            }
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await RefreshAllStatusAsync();
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
