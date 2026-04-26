using Avalonia;
using Avalonia.ReactiveUI;
using System;

namespace DSPiCliClient;

class Program
{
    // Initialization code. Don't use any Avalonia, xaml, etc before it's called.
    // Args can be used for customization of AppBuilder, e.g. .With(new Win32PlatformOptions { ... })
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // AppBuilder can be configured here:
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}
