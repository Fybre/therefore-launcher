using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace ThereforeLauncher;

public sealed record ClientProcess(string Name, int Id, int SessionId, bool HasWindow, bool HasExited, string? Executable)
{
    public bool BlocksLaunch(int currentSession, string installation)
    {
        if (HasExited || SessionId != currentSession) return false;
        if (Executable is not null && !string.Equals(Path.GetDirectoryName(Executable)?.TrimEnd('\\'), Path.GetFullPath(installation).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
        // Viewer and Loader can remain as background helpers after their UI closes.
        // Hidden primary clients still block: they may be starting or signing in.
        return HasWindow || Name is not ("TheViewer" or "TheLoader");
    }
}

public static class ClientDetection
{
    public static List<ClientProcess> FindBlocking(string installation)
    {
        using var current = Process.GetCurrentProcess();
        var result = new List<ClientProcess>();
        foreach (var name in new[] { "TheNavigator", "TheDesigner", "TheConsole", "TheViewer", "TheCapture", "TheCaseMgr", "TheLoader" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        if (process.HasExited || process.SessionId != current.SessionId) continue;
                        string? executable = null;
                        try { executable = process.MainModule?.FileName; } catch (Win32Exception) { /* Retain conservative detection if the path is inaccessible. */ }
                        var candidate = new ClientProcess(name, process.Id, process.SessionId, process.MainWindowHandle != IntPtr.Zero, process.HasExited, executable);
                        if (candidate.BlocksLaunch(current.SessionId, installation)) result.Add(candidate);
                    }
                    catch (InvalidOperationException) { /* Process exited while being inspected. */ }
                    catch (Win32Exception ex)
                    {
                        throw new InvalidOperationException($"Could not inspect {name} (PID {process.Id}): {ex.Message}", ex);
                    }
                }
            }
        }
        return result;
    }
}
