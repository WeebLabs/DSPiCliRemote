using System.IO;
using DSPiCliServer.ViewModels;

namespace DSPiCliServer.Services;

public static class HtmlPages
{
    private static string _indexHtml = string.Empty;
    private static string _cliHtml = string.Empty;

    private static string ReadHtmlFile(string rootPath, string fname)
    {
        string result = string.Empty;
        try
        {
            string indexPath = Path.Combine(rootPath, "wwwroot", fname);
            if (File.Exists(indexPath))
            {
                result = File.ReadAllText(indexPath);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        return result;
    }

    public static void LoadPages(string rootPath)
    {
        _indexHtml = ReadHtmlFile(rootPath, "index.html");
        _cliHtml = ReadHtmlFile(rootPath, "cli.html");
    }

    public static string GetIndexHtml(string ipAddress)
    {
        if (string.IsNullOrEmpty(_indexHtml)) 
            return "";
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
        if (string.IsNullOrEmpty(_cliHtml)) 
            return "";
        return _cliHtml;
    }
}