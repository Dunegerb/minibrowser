using CefSharp;
using CefSharp.Wpf;
using MiniBrowser.Core;
using System.IO;
using System.Windows;

namespace MiniBrowser;

public partial class App : Application
{
    public RuntimeServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiniBrowser");
        Directory.CreateDirectory(appRoot);

        CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

        var cefSettings = new CefSettings
        {
            CachePath = Path.Combine(appRoot, "profile"),
            LogFile = Path.Combine(appRoot, "logs", "cef.log"),
            LogSeverity = LogSeverity.Warning
        };

        Directory.CreateDirectory(Path.GetDirectoryName(cefSettings.LogFile)!);

        // Conservative privacy-oriented switches. These affect browser-owned background work;
        // they do not and cannot prevent websites themselves from making network requests.
        cefSettings.CefCommandLineArgs["disable-background-networking"] = "1";
        cefSettings.CefCommandLineArgs["disable-component-update"] = "1";
        cefSettings.CefCommandLineArgs["disable-domain-reliability"] = "1";
        cefSettings.CefCommandLineArgs["disable-sync"] = "1";
        cefSettings.CefCommandLineArgs["disable-default-apps"] = "1";
        cefSettings.CefCommandLineArgs["no-first-run"] = "1";
        cefSettings.CefCommandLineArgs["disable-breakpad"] = "1";

        if (Cef.IsInitialized == null)
        {
            var initialized = Cef.Initialize(cefSettings, performDependencyCheck: true, browserProcessHandler: null);
            if (!initialized)
            {
                MessageBox.Show(
                    $"Falha ao inicializar CEF ({Cef.GetExitCode()}). Consulte o log local em {cefSettings.LogFile}.",
                    "MiniBrowser",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(2);
                return;
            }
        }

        Services = RuntimeServices.CreateDefault(appRoot);

        var window = new MainWindow(Services);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services?.Dispose();
        base.OnExit(e);
    }
}
