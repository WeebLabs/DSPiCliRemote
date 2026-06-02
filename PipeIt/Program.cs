using PortAudioSharp;

using System.Runtime.InteropServices;

namespace PipeIt;

class Program
{
    // Keeping a reference to the callback to prevent GC
    private static readonly PortAudioSharp.Stream.Callback _callback = MyCallback;
    private static int _channels = 2;
    private static int[] _allowedRates = { 44100, 48000, 88200, 96000, 192000 };

    static void Main(string[] args)
    {
        var rslt = false;
        while (!rslt)
        {
            rslt = MainSub(args);   // return true if user asked to stop
            if (!rslt)
            {
                Console.WriteLine("PipeIt - Restarting");
                Thread.Sleep(2000);
            }
        }
    }

    static SampleFormat BitsToBits(int bitsize)
    {
        switch (bitsize)
        {
            case 32 :
                return SampleFormat.Float32;
            case 24 :
                return SampleFormat.Int24;
            case 16 :
                return SampleFormat.Int16;
            default :
                throw new ArgumentException("Unsupported bit size");
        }
    }

    static bool MainSub(string[] args)
    {
        Console.WriteLine("PipeIt - Audio Loopback for macOS. [-i in] [-o out] [-r rate] [-b sizebits]");

        try
        {
            PortAudio.Initialize();
            Console.WriteLine("PortAudio Initialized.");

            int deviceCount = PortAudio.DeviceCount;
            if (deviceCount == 0)
            {
                Console.WriteLine("No audio devices found.");
                return false;
            }

            Console.WriteLine($"Found {deviceCount} devices:");
            for (int i = 0; i < deviceCount; i++)
            {
                var deviceInfo = PortAudio.GetDeviceInfo(i);
                Console.WriteLine($"[{i}] {deviceInfo.name} (In: {deviceInfo.maxInputChannels}, Out: {deviceInfo.maxOutputChannels})");
            }

            int inputDevice = -1;
            int outputDevice = -1;
            int rateOfSpeed = 96000;        // default 96KHz
            SampleFormat bitSize = SampleFormat.Int16;               // 16 bit int default?

            if (args.Length >= 2)
            {
                for (int i = 0; i < (args.Length-1); i++)
                {
                    if (args[i].Length == 2 && '-' == args[i][0])
                    {
                        switch (args[i][1])
                        {
                            case 'i' : // input device
                                if (int.TryParse(args[i+1], out int inIdx)) 
                                    inputDevice = inIdx;
                                i++;
                                break;
                            case 'o' : // output device
                                if (int.TryParse(args[i+1], out int outIdx)) 
                                    outputDevice = outIdx;
                                i++;
                                break;
                            case 'r' : // rate of speed
                                if (int.TryParse(args[i + 1], out int rate))
                                {
                                    // assume rates below 200 are in khz
                                    if (rate < 200)
                                        rate *= 1000;
                                    if(_allowedRates.Contains(rate))
                                        rateOfSpeed = rate;
                                    else
                                    {
                                        Console.WriteLine($"Invalid bit rate of {rate}");
                                        string ratelist = string.Join(',', _allowedRates.Select(x => x.ToString()));
                                        Console.WriteLine($"Pick from {ratelist}");
                                    }
                                }
                                i++;
                                break;
                            case 'b' : // size in bits
                                if (int.TryParse(args[i+1], out int bits))
                                    bitSize = BitsToBits(bits);
                                i++;
                                break;
                            default:
                                Console.WriteLine($"Unknown argument: {args[i]}");
                                return false;
                        }
                    }
                }
            }

            if (inputDevice == -1) 
                inputDevice = PortAudio.DefaultInputDevice;
            if (outputDevice == -1) 
                outputDevice = PortAudio.DefaultOutputDevice;

            if (inputDevice == PortAudio.NoDevice || outputDevice == PortAudio.NoDevice)
            {
                Console.WriteLine("Error: Could not find default input or output device.");
                return false;
            }

            var inputDeviceInfo = PortAudio.GetDeviceInfo(inputDevice);
            var outputDeviceInfo = PortAudio.GetDeviceInfo(outputDevice);

            Console.WriteLine($"Using Input: [{inputDevice}] {inputDeviceInfo.name}");
            Console.WriteLine($"Using Output: [{outputDevice}] {outputDeviceInfo.name}");

            // Common audio parameters
            double sampleRate = rateOfSpeed;
            uint framesPerBuffer = 256; 
            _channels = Math.Min(inputDeviceInfo.maxInputChannels, outputDeviceInfo.maxOutputChannels);
            _channels = Math.Max(1, Math.Min(_channels, 2)); // Use Mono or Stereo

            StreamParameters inputParameters = new StreamParameters();
            inputParameters.device = inputDevice;
            inputParameters.channelCount = _channels;
            inputParameters.sampleFormat = bitSize;
            inputParameters.suggestedLatency = inputDeviceInfo.defaultLowInputLatency;
            inputParameters.hostApiSpecificStreamInfo = IntPtr.Zero;

            StreamParameters outputParameters = new StreamParameters();
            outputParameters.device = outputDevice;
            outputParameters.channelCount = _channels;
            outputParameters.sampleFormat = bitSize;
            outputParameters.suggestedLatency = outputDeviceInfo.defaultLowOutputLatency;
            outputParameters.hostApiSpecificStreamInfo = IntPtr.Zero;
            
            using (var stream = new PortAudioSharp.Stream(inputParameters, outputParameters, sampleRate, framesPerBuffer, StreamFlags.NoFlag, _callback, IntPtr.Zero))
            {
                stream.Start();
                Console.WriteLine($"Piping audio: ({_channels} channels) at {rateOfSpeed}.{bitSize}. Press Enter to stop...");
                Console.ReadLine();
                stream.Stop();
            }

            Console.WriteLine("Stopped.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return false;
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
