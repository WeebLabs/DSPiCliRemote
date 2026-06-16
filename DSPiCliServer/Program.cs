using System.Reflection;
using DSPiCliServer.Services;
using DSPiCliServer.ViewModels;
using DSPiConsole.Usb;

namespace DSPiCliServer;

sealed class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Syntax: DSPiCliServer [httpPort] [clientPort]");
        Console.WriteLine("Default: DSPiCliServer 8082 8084");
        
        // set the path to the current executable directory
        // to ensure all dlls are loaded
        string? exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if(!string.IsNullOrEmpty(exeDir))
        {
            try
            {
                Directory.SetCurrentDirectory(exeDir);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
        int port = 8084;
        int httpPort = 8082;
        if (args.Length > 1 && int.TryParse(args[1], out int customPort))
        {
            port = customPort;
        }
        if (args.Length > 0 && int.TryParse(args[0], out int customHttpPort))
        {
            httpPort = customHttpPort;
        }
        Console.WriteLine("DSPi CLI Server starting...");
        Console.WriteLine("Press Ctrl+C to stop the server.");
        Console.WriteLine($"Using: DSPiCliServer http={httpPort} client={port}");

        // this starts everything up
        var vm = new MainWindowViewModel(port, httpPort);

        Console.WriteLine("Press Ctrl+C to stop the server.");

        // var tcs = new TaskCompletionSource();
        // Console.CancelKeyPress += (s, e) =>
        // {
        //     e.Cancel = true;
        //     tcs.SetResult();
        // };
        //
        // await tcs.Task;
        bool exitRequested = false;
        // Subscribe to the CancelKeyPress event
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("\nCtrl+C detected. Cleaning up...");

            // Prevent the process from terminating immediately
            e.Cancel = true;

            // Set the exit flag
            exitRequested = true;
        };

        // Main loop
        int pick = 0;
        while (!exitRequested)
        {
            // wait 3000ms and draw a .
            if (6 <= pick++)
            {
                Console.Write(".");
                pick = 0;
            }
            await Task.Delay(1500);
        }        
        
        Console.WriteLine("Stopping server...");
        vm.StopServices();
    }

    private static void OnDeviceConnectionChanged(DeviceManager devService)
    {
        if (devService.IsConnected)
        {
            var info = devService.MyDevice.GetDeviceInfo();
            var newPlatform = info?.Platform ?? "";
            devService.MyDevice.NumChannels = newPlatform == "RP2350" ? 11 : 7;
            
            var bulk = devService.MyDevice.GetAllParams();
            if (bulk != null)
            {
                var parsed = BulkParamsParser.Parse(bulk);
                if (parsed != null)
                {
                    var infos = $"Loudness={parsed.LoudnessEnabled}, PreampGain={parsed.PreampGainDb}";
                    Console.WriteLine($"[DEVICE] Connected: {infos}");
                    // TcpServerService.Server?.WriteClientAsync(infos).Wait(); // This method might be buggy but keeping logic similar
                }
            }
        }
        else
        {
            Console.WriteLine("[DEVICE] Disconnected.");
        }
    }
}
