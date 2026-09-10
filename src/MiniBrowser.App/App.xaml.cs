using CefSharp;
using CefSharp.Wpf;
using CefRuntime = CefSharp.Cef;
using MiniBrowser.Core;
using MiniBrowser.Core.Capsules;
using MiniBrowser.Core.Storage;
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

        StoragePolicy.PrepareProfileBeforeCefStarts(appRoot);
        CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

        var rootCachePath = CapsuleManager.GetRootCachePath(appRoot);
        var globalCachePath = CapsuleManager.GetGlobalCachePath(appRoot);
        Directory.CreateDirectory(globalCachePath);

        var cefSettings = new CefSettings
        {
            // V0.6 keeps the global context under a dedicated root. Tabs use per-capsule
            // RequestContexts; the global context is intentionally not the identity store.
            RootCachePath = rootCachePath,
            CachePath = globalCachePath,
            LogFile = Path.Combine(appRoot, "logs", "cef.log"),
            LogSeverity = LogSeverity.Error,
            Locale = "pt-BR",
            AcceptLanguageList = "pt-BR,pt,en-US,en",
            WindowlessRenderingEnabled = false
        };

        Directory.CreateDirectory(Path.GetDirectoryName(cefSettings.LogFile)!);

        // HwndHost uses native window rendering. Keep GPU composition enabled.
        cefSettings.CefCommandLineArgs.Remove("disable-gpu-compositing");
        cefSettings.CefCommandLineArgs["disk-cache-size"] = StoragePolicy.HttpCacheBudgetBytes.ToString();
        cefSettings.CefCommandLineArgs["gpu-disk-cache-size-kb"] = "8192";

        // No MiniBrowser-owned cloud services/analytics. These switches also reduce Chromium
        // background services, but external packet capture remains the source of truth.
        cefSettings.CefCommandLineArgs["disable-background-networking"] = "1";
        cefSettings.CefCommandLineArgs["disable-component-update"] = "1";
        cefSettings.CefCommandLineArgs["disable-domain-reliability"] = "1";
        cefSettings.CefCommandLineArgs["disable-sync"] = "1";
        cefSettings.CefCommandLineArgs["disable-default-apps"] = "1";
        cefSettings.CefCommandLineArgs["no-first-run"] = "1";
        cefSettings.CefCommandLineArgs["disable-breakpad"] = "1";

        if (CefRuntime.IsInitialized == null)
        {
            var initialized = CefRuntime.Initialize(cefSettings, performDependencyCheck: true, browserProcessHandler: null);
            if (!initialized)
            {
                MessageBox.Show(
                    $"Falha ao inicializar CEF ({CefRuntime.GetExitCode()}). Consulte {cefSettings.LogFile}.",
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
