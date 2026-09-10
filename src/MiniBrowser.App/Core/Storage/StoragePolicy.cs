using System.IO;

namespace MiniBrowser.Core.Storage;

public static class StoragePolicy
{
    public const long HttpCacheBudgetBytes = 64L * 1024 * 1024;
    public const long AuditFileBytes = 8L * 1024 * 1024;
    public const int AuditFileCount = 3;
    public const int SessionHistoryLimit = 300;
    public const long CefLogBudgetBytes = 4L * 1024 * 1024;

    // V0.6: anonymous capsules use in-memory RequestContext storage. Persistent capsules retain
    // cookies/localStorage/IndexedDB, but volatile Chromium caches are trimmed before CEF starts.
    // The legacy V0.5 profile is also trimmed but never deleted wholesale, avoiding silent loss
    // of old user data while keeping its cache from consuming unbounded disk.
    public static void PrepareProfileBeforeCefStarts(string appRoot)
    {
        var roots = new[]
        {
            Path.Combine(appRoot, "cef-root"),
            Path.Combine(appRoot, "profile") // legacy V0.5 location
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            TrimVolatileChromiumState(root);
        }

        Directory.CreateDirectory(Path.Combine(appRoot, "cef-root"));

        var logDirectory = Path.Combine(appRoot, "logs");
        DeleteOldFiles(logDirectory, TimeSpan.FromDays(7));
        DeleteIfOversized(Path.Combine(logDirectory, "cef.log"), CefLogBudgetBytes);
    }

    private static void TrimVolatileChromiumState(string profileRoot)
    {
        DeleteDirectoriesByName(profileRoot, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cache",
            "Code Cache",
            "GPUCache",
            "DawnCache",
            "DawnGraphiteCache",
            "GrShaderCache",
            "ShaderCache",
            "Media Cache",
            "CacheStorage",
            "blob_storage"
        });

        DeleteFilesByName(profileRoot, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "History",
            "History-journal",
            "Visited Links",
            "Top Sites",
            "Top Sites-journal"
        });
    }

    private static void DeleteDirectoriesByName(string root, HashSet<string> names)
    {
        if (!Directory.Exists(root)) return;

        foreach (var dir in SafeEnumerateDirectories(root))
        {
            if (names.Contains(Path.GetFileName(dir)))
            {
                TryDeleteDirectory(dir);
                continue;
            }

            DeleteDirectoriesByName(dir, names);
        }
    }

    private static void DeleteFilesByName(string root, HashSet<string> names)
    {
        if (!Directory.Exists(root)) return;

        foreach (var file in SafeEnumerateFiles(root))
        {
            if (names.Contains(Path.GetFileName(file)))
            {
                try { File.Delete(file); } catch { }
            }
        }

        foreach (var dir in SafeEnumerateDirectories(root))
        {
            DeleteFilesByName(dir, names);
        }
    }

    private static void DeleteIfOversized(string path, long maxBytes)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > maxBytes) File.Delete(path);
        }
        catch { }
    }

    private static void DeleteOldFiles(string directory, TimeSpan maxAge)
    {
        if (!Directory.Exists(directory)) return;
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var file in SafeEnumerateFiles(directory))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
            }
            catch { }
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try { return Directory.EnumerateDirectories(path).ToArray(); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string path)
    {
        try { return Directory.EnumerateFiles(path).ToArray(); }
        catch { return Array.Empty<string>(); }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }
}
