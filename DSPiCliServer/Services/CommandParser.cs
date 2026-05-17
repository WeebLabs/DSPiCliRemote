using System.Text;
using System.Diagnostics;
using DSPiConsole.Usb;

namespace DSPiCliServer.Services;

public static class CommandParser
{
    private static string? _storedString = "Play -d";
    private static Process? _runningProcess;
    private static readonly object _processLock = new();

    public static string ProcessCommandPublic(string input) => ProcessCommand(input);

    public static void TestRun()
    {
        Console.WriteLine("[TestRun] Starting CommandParser tests...");
        
        // Use a safe command that works on most systems
        // On Windows 'whoami' or 'ping localhost -n 1'
        // On Linux 'whoami' or 'ping localhost -c 1'
        string testCmd = OperatingSystem.IsWindows() ? "whoami" : "whoami";
        
        Console.WriteLine($"[TestRun] Testing set_str with: {testCmd}");
        string res1 = ProcessCommand($"set_str {testCmd}");
        Console.WriteLine($"[TestRun] Result: {res1}");

        Console.WriteLine("[TestRun] Testing run_str...");
        string res2 = ProcessCommand("run_str");
        Console.WriteLine($"[TestRun] Result: {res2}");

        Console.WriteLine("[TestRun] Testing is_running...");
        string res3 = ProcessCommand("is_running");
        Console.WriteLine($"[TestRun] Result: {res3}");

        // Wait a bit for the process to potentially finish if it's quick
        Thread.Sleep(500);

        Console.WriteLine("[TestRun] Testing kill_str...");
        string res4 = ProcessCommand("kill_str");
        Console.WriteLine($"[TestRun] Result: {res4}");

        Console.WriteLine("[TestRun] Testing is_running after kill...");
        string res5 = ProcessCommand("is_running");
        Console.WriteLine($"[TestRun] Result: {res5}");

        Console.WriteLine("[TestRun] Testing help...");
        string resHelp = ProcessCommand("help");
        Console.WriteLine($"[TestRun] Result: {resHelp}");

        Console.WriteLine("[TestRun] Testing get_input (mocked as not connected or error if no device)...");
        string resGetInput = ProcessCommand("get_input");
        Console.WriteLine($"[TestRun] Result: {resGetInput}");

        Console.WriteLine("[TestRun] Testing set_input invalid...");
        string resSetInputInv = ProcessCommand("set_input invalid");
        Console.WriteLine($"[TestRun] Result: {resSetInputInv}");
        
        Console.WriteLine("[TestRun] CommandParser tests completed.");
    }

    private static string ProcessCommand(string input)
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
                "help" => "Available commands: ping, time, date, help, hello, get_all, get_vol, set_vol <db>, get_bypass, set_bypass <0/1>, get_loudness, set_loudness <0/1>, get_leveling, set_leveling <0/1>, get_crossfeed, set_crossfeed <0/1>, get_samplerate, get_deviceid, get_firmwareversion, get_activepreset, get_presets, set_preset, get_input, set_input <usb/spdif>, get_str, set_str <val>, run_str, kill_str, is_running",
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
                "get_all" => GetAllStatus(dv),
                "get_deviceid" => dv.IsConnected ? (dv.MyDevice.GetDeviceSerial() ?? "Unknown") : "Not connected",
                "get_firmwareversion" => dv.IsConnected ? (dv.MyDevice.GetDeviceInfo()?.FirmwareVersion ?? "Unknown") : "Not connected",
                "get_activepreset" => dv.IsConnected ? dv.MyDevice.GetActivePreset().ToString() : "Not connected",
                "get_presets" => GetPresetsCommand(dv),
                "set_preset" => (dv.IsConnected && parts.Length > 1 && int.TryParse(parts[1], out int slot)) ? (dv.MyDevice.LoadPreset(slot) == 0 ? "OK" : "Error") : "Error",
                "get_input" => dv.IsConnected ? (dv.MyDevice.GetInputSource()?.ToString() ?? "Error") : "Not connected",
                "set_input" => (dv.IsConnected && parts.Length > 1) ? (SetInputSource(dv, parts[1])) : "Error",
                "cto" => ControlTransferOut(dv, parts),
                "cti" => ControlTransferIn(dv, parts),
                "get_peaks" => GetPeaks(dv),
                "get_str" => _storedString ?? "None",
                "set_str" => SetStr(input),
                "run_str" => RunStr(),
                "kill_str" => KillStr(),
                "is_running" => IsRunning(),
                _ => $"Echo: {input}"
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string SetInputSource(DeviceManager dv, string sourceStr)
    {
        if (sourceStr.ToLower() == "usb")
        {
            return dv.MyDevice.SetInputSource(InputSource.Usb) ? "OK" : "Error";
        }
        else if (sourceStr.ToLower() == "spdif")
        {
            return dv.MyDevice.SetInputSource(InputSource.Spdif) ? "OK" : "Error";
        }
        return "Error: Invalid source. Use 'usb' or 'spdif'.";
    }

    private static string ControlTransferOut(DeviceManager dv, string[] parts)
    {
        if (!dv.IsConnected) return "Not connected";
        if (parts.Length < 3) return "Error: Missing request and value";

        if (!byte.TryParse(parts[1], out byte request)) return "Error: Invalid request";
        if (!ushort.TryParse(parts[2], out ushort value)) return "Error: Invalid value";

        byte[]? data = null;
        if (parts.Length > 3)
        {
            try
            {
                data = Convert.FromHexString(parts[3]);
            }
            catch { return "Error: Invalid hex data"; }
        }

        bool result = dv.MyDevice.ControlTransferOut(request, value, data);
        return result ? "OK" : "Error";
    }

    private static string ControlTransferIn(DeviceManager dv, string[] parts)
    {
        if (!dv.IsConnected) return "Not connected";
        if (parts.Length < 4) return "Error: Missing request, value, and length";

        if (!byte.TryParse(parts[1], out byte request)) return "Error: Invalid request";
        if (!ushort.TryParse(parts[2], out ushort value)) return "Error: Invalid value";
        if (!int.TryParse(parts[3], out int length)) return "Error: Invalid length";

        try
        {
            var result = dv.MyDevice.ControlTransferIn(request, value, length);
            return result != null ? Convert.ToHexString(result) : "Error";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string GetPeaks(DeviceManager dv)
    {
        if (!dv.IsConnected) return "Not connected";
        var status = dv.MyDevice.GetStatus();
        if (status == null) return "Error";

        // Packet format: numChannels*2 (peaks) + 2 (CPU) + 2 (Clip)
        int numCh = dv.MyDevice.NumChannels;
        byte[] buffer = new byte[numCh * 2 + 4];
        for (int i = 0; i < numCh; i++)
        {
            ushort val = (ushort)(status.Peaks[i] * 32767.0f);
            BitConverter.TryWriteBytes(buffer.AsSpan(i * 2), val);
        }
        buffer[numCh * 2] = (byte)status.Cpu0Load;
        buffer[numCh * 2 + 1] = (byte)status.Cpu1Load;
        BitConverter.TryWriteBytes(buffer.AsSpan(numCh * 2 + 2), status.ClipFlags);

        return Convert.ToHexString(buffer);
    }

    private static string SetStr(string input)
    {
        var prefix = "set_str ";
        if (input.Length <= prefix.Length) return "Error: Missing string value";
        _storedString = input.Substring(prefix.Length).Trim();
        return $"OK: Stored string = {_storedString}";
    }

    private static string RunStr()
    {
        if (string.IsNullOrWhiteSpace(_storedString)) 
            return "Error: No string stored. Use set_str first.";

        lock (_processLock)
        {
            if (_runningProcess is { HasExited: false })
            {
                return "Error: A process is already running. Kill it first.";
            }

            try
            {
                // We need to handle commands that might have arguments
                var parts = _storedString.Split(' ', 2);
                var fileName = parts[0];
                var arguments = parts.Length > 1 ? parts[1] : "";

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true, // Use shell to support built-ins or scripts if needed
                    CreateNoWindow = true
                };

                _runningProcess = Process.Start(startInfo);
                if (_runningProcess == null) 
                    return "Error: Failed to start process.";
                
                return $"OK: Started process with PID {_runningProcess.Id}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }

    private static string KillStr()
    {
        lock (_processLock)
        {
            if (_runningProcess == null || _runningProcess.HasExited)
            {
                return "Error: No process is running.";
            }

            try
            {
                _runningProcess.Kill(true); // Kill with children
                return "OK: Process terminated.";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }

    private static string IsRunning()
    {
        lock (_processLock)
        {
            bool running = _runningProcess is { HasExited: false };
            return running ? "True" : "False";
        }
    }
    
    private static string GetAllStatus(DeviceManager dv)
    {
        if (!dv.IsConnected) return "Not connected";

        var bulk = dv.MyDevice.GetAllParams();
        if (bulk == null) return "Error: Failed to fetch params";

        var parsed = BulkParamsParser.Parse(bulk);
        if (parsed == null) return "Error: Failed to parse params";

        var sb = new StringBuilder();
        // Master Volume (prefer BulkParams value if available, but DspDevice has it too)
        float? vol = parsed.HasMasterVolume ? parsed.MasterVolumeDb : dv.MyDevice.GetMasterVolume();
        sb.Append("vol=").Append(vol?.ToString("F1") ?? "Error").Append(';');

        // Input Source
        string input = parsed.HasInputConfig ? (parsed.InputSource == 0 ? "Usb" : "Spdif") : (dv.MyDevice.GetInputSource()?.ToString() ?? "Error");
        sb.Append("input=").Append(input).Append(';');

        // Bypass
        sb.Append("bypass=").Append(parsed.Bypass ? "1" : "0").Append(';');

        // Loudness
        sb.Append("loudness=").Append(parsed.LoudnessEnabled ? "1" : "0").Append(';');

        // Leveling
        sb.Append("leveling=").Append(parsed.LevellerEnabled ? "1" : "0").Append(';');

        // Crossfeed
        sb.Append("crossfeed=").Append(parsed.CrossfeedEnabled ? "1" : "0").Append(';');

        // Preamp (Global)
        sb.Append("preamp=").Append(parsed.PreampGainDb.ToString("F1")).Append(';');

        // Active Preset
        int active = dv.MyDevice.GetActivePreset();
        sb.Append("preset=").Append(active).Append(';');

        // Sample Rate (still needs a status call, but it's okay)
        uint? rate = dv.MyDevice.GetStatusUInt32(15);
        sb.Append("samplerate=").Append(rate?.ToString() ?? "0");

        return sb.ToString();
    }

    private static string DoHello()
    {
        var dv = DeviceManager.Instance;
        if (dv.IsConnected)
        {
            return GetAllStatus(dv);
        }
        return "Not connected";
    }


    private static string GetPresetsCommand(DeviceManager dv)
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