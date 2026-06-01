using PortAudioSharp;

using System.Runtime.InteropServices;

namespace PipeIt;

class Program
{
    // Keeping a reference to the callback to prevent GC
    private static readonly PortAudioSharp.Stream.Callback _callback = MyCallback;
    private static int _channels = 2;

    static void Main(string[] args)
    {
        while (true)
        {
            MainSub(args);
            Console.WriteLine("PipeIt - Restarting");
            Thread.Sleep(2000);
        }
    }

    static void MainSub(string[] args)
    {
        Console.WriteLine("PipeIt - Audio Loopback for macOS (and others)");

        try
        {
            PortAudio.Initialize();
            Console.WriteLine("PortAudio Initialized.");

            int deviceCount = PortAudio.DeviceCount;
            if (deviceCount == 0)
            {
                Console.WriteLine("No audio devices found.");
                return;
            }

            Console.WriteLine($"Found {deviceCount} devices:");
            for (int i = 0; i < deviceCount; i++)
            {
                var deviceInfo = PortAudio.GetDeviceInfo(i);
                Console.WriteLine($"[{i}] {deviceInfo.name} (In: {deviceInfo.maxInputChannels}, Out: {deviceInfo.maxOutputChannels})");
            }

            int inputDevice = -1;
            int outputDevice = -1;

            if (args.Length >= 2)
            {
                if (int.TryParse(args[0], out int inIdx)) 
                    inputDevice = inIdx;
                if (int.TryParse(args[1], out int outIdx)) 
                    outputDevice = outIdx;
            }

            if (inputDevice == -1) 
                inputDevice = PortAudio.DefaultInputDevice;
            if (outputDevice == -1) 
                outputDevice = PortAudio.DefaultOutputDevice;

            if (inputDevice == PortAudio.NoDevice || outputDevice == PortAudio.NoDevice)
            {
                Console.WriteLine("Error: Could not find default input or output device.");
                return;
            }

            var inputDeviceInfo = PortAudio.GetDeviceInfo(inputDevice);
            var outputDeviceInfo = PortAudio.GetDeviceInfo(outputDevice);

            Console.WriteLine($"Using Input: [{inputDevice}] {inputDeviceInfo.name}");
            Console.WriteLine($"Using Output: [{outputDevice}] {outputDeviceInfo.name}");

            // Common audio parameters
            double sampleRate = 96000;
            uint framesPerBuffer = 256; 
            _channels = Math.Min(inputDeviceInfo.maxInputChannels, outputDeviceInfo.maxOutputChannels);
            _channels = Math.Max(1, Math.Min(_channels, 2)); // Use Mono or Stereo

            StreamParameters inputParameters = new StreamParameters();
            inputParameters.device = inputDevice;
            inputParameters.channelCount = _channels;
            inputParameters.sampleFormat = SampleFormat.Float32;
            inputParameters.suggestedLatency = inputDeviceInfo.defaultLowInputLatency;
            inputParameters.hostApiSpecificStreamInfo = IntPtr.Zero;

            StreamParameters outputParameters = new StreamParameters();
            outputParameters.device = outputDevice;
            outputParameters.channelCount = _channels;
            outputParameters.sampleFormat = SampleFormat.Float32;
            outputParameters.suggestedLatency = outputDeviceInfo.defaultLowOutputLatency;
            outputParameters.hostApiSpecificStreamInfo = IntPtr.Zero;

            using (var stream = new PortAudioSharp.Stream(inputParameters, outputParameters, sampleRate, framesPerBuffer, StreamFlags.NoFlag, _callback, IntPtr.Zero))
            {
                stream.Start();
                Console.WriteLine($"Piping audio ({_channels} channels). Press Enter to stop...");
                Console.ReadLine();
                stream.Stop();
            }

            Console.WriteLine("Stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            PortAudio.Terminate();
        }
    }

    private static StreamCallbackResult MyCallback(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        if (input != IntPtr.Zero && output != IntPtr.Zero)
        {
            // Number of bytes to copy = frames * channels * sizeof(float)
            int byteCount = (int)frameCount * _channels * sizeof(float);
            
            // Using a simple buffer copy
            byte[] buffer = new byte[byteCount];
            Marshal.Copy(input, buffer, 0, byteCount);
            Marshal.Copy(buffer, 0, output, byteCount);
        }
        
        return StreamCallbackResult.Continue;
    }
}
