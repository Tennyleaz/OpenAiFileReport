using Bluegrams.Application;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace OpenAiFileReport;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAiTempFiles");
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);
        const string configName = "settings.config";
        PortableSettingsProvider.SettingsFileName = configName;
        PortableSettingsProvider.SettingsDirectory = configDir;
        PortableSettingsProvider.ApplyProvider(OpenAiFileReport.Properties.Settings.Default);
    }
}

