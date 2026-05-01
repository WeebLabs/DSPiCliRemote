using System.IO;
using DSPiCliServer.ViewModels;

namespace DSPiCliServer.Services;

public static class HtmlPages
{
    private static string _indexHtml = string.Empty;
    private static string _cliHtml = string.Empty;

    public static void LoadPages(string rootPath)
    {
        try
        {
            string indexPath = Path.Combine(rootPath, "wwwroot", "index.html");
            if (File.Exists(indexPath))
            {
                _indexHtml = File.ReadAllText(indexPath);
            }

            string cliPath = Path.Combine(rootPath, "wwwroot", "cli.html");
            if (File.Exists(cliPath))
            {
                _cliHtml = File.ReadAllText(cliPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading HTML pages: {ex.Message}");
        }
    }

    public static string GetIndexHtml(string ipAddress)
    {
        if (string.IsNullOrEmpty(_indexHtml)) return "";
        var pstr = _indexHtml.Replace("{ipAddress}", ipAddress);
        pstr = pstr.Replace("{myVersion}", MainWindowViewModel.Version);
        var fwVersion = DeviceManager.Instance.IsConnected 
            ? (DeviceManager.Instance.MyDevice.GetDeviceInfo()?.FirmwareVersion ?? "Unknown") 
            : "Not connected";
        pstr = pstr.Replace("{fwVersion}", fwVersion);
        return pstr;
    }

    public static string GetCliHtml()
    {
        if (string.IsNullOrEmpty(_cliHtml)) return "";
        return _cliHtml;
    }
}