using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Photino.NET;

namespace EgyptTax.Desktop;

/// <summary>
/// v5 desktop shell — launches DaftarX in a native window without
/// requiring the operator to open a browser. Web mode (running
/// EgyptTax.Web.exe directly + opening localhost in a browser) stays
/// fully supported; this is purely an alternative front-end that
/// boots the same Kestrel binary as a child process and points a
/// Photino window (WebView2 on Windows) at it.
///
/// Lifecycle:
///   1. Pick a free localhost TCP port.
///   2. Spawn EgyptTax.Web.exe with --urls and --environment.
///   3. Poll the port until Kestrel is accepting connections (10s
///      timeout) so the window doesn't open on a blank "site can't
///      be reached" page.
///   4. Open the Photino window at http://localhost:{port}/.
///   5. On window close: kill the child process tree so Kestrel
///      doesn't keep running headless after the user clicks X.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var webExe = ResolveWebExecutable();
            if (webExe is null)
            {
                ShowFatal(
                    "DaftarX.exe couldn't find EgyptTax.Web.exe.\n\n"
                    + "Expected it next to DaftarX.exe under .\\web\\EgyptTax.Web.exe "
                    + "or in the sibling EgyptTax.Web/bin/{Configuration}/net8.0/ folder.");
                return 2;
            }

            var port = FindFreeTcpPort();
            var url = $"http://127.0.0.1:{port}/";
            var web = StartWebProcess(webExe, port);

            try
            {
                if (!WaitForPort("127.0.0.1", port, TimeSpan.FromSeconds(20)))
                {
                    ShowFatal(
                        $"Web host did not start on port {port} within 20s.\n\n"
                        + "Run EgyptTax.Web.exe from a terminal to see the actual startup error.");
                    SafeKill(web);
                    return 3;
                }

                new PhotinoWindow()
                    .SetTitle("DaftarX")
                    .SetUseOsDefaultLocation(false)
                    .SetSize(1400, 900)
                    .Center()
                    .RegisterWindowClosingHandler((object sender, EventArgs e) =>
                    {
                        SafeKill(web);
                        return false; // false = allow close
                    })
                    .Load(new Uri(url))
                    .WaitForClose();

                return 0;
            }
            finally
            {
                SafeKill(web);
            }
        }
        catch (Exception ex)
        {
            ShowFatal("DaftarX shell crashed:\n\n" + ex);
            return 1;
        }
    }

    /// <summary>Finds <c>EgyptTax.Web.exe</c> using a small search list:
    /// (1) <c>./web/EgyptTax.Web.exe</c> next to the launcher (production
    /// layout), (2) the sibling Web project's bin folder (dev layout),
    /// (3) PATH-resolved <c>EgyptTax.Web.exe</c>.</summary>
    private static string? ResolveWebExecutable()
    {
        var baseDir = AppContext.BaseDirectory;

        // 1. Production layout: web binaries copied next to the launcher.
        var bundled = Path.Combine(baseDir, "web", "EgyptTax.Web.exe");
        if (File.Exists(bundled)) return bundled;

        // 2. Dev layout: src/EgyptTax.Desktop/bin/{Cfg}/net8.0-windows/
        //    has a sibling src/EgyptTax.Web/bin/{Cfg}/net8.0/.
        //    Walk up from baseDir to find it.
        var dir = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            var src = dir.Parent;
            if (src is null) continue;
            foreach (var cfg in new[] { "Debug", "Release" })
            {
                var devCandidate = Path.Combine(
                    src.FullName, "EgyptTax.Web", "bin", cfg, "net8.0", "EgyptTax.Web.exe");
                if (File.Exists(devCandidate)) return devCandidate;
            }
        }

        // 3. PATH fallback.
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in pathDirs)
        {
            try
            {
                var candidate = Path.Combine(p, "EgyptTax.Web.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* skip malformed PATH entries */ }
        }

        return null;
    }

    private static int FindFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static Process StartWebProcess(string webExe, int port)
    {
        var psi = new ProcessStartInfo
        {
            FileName = webExe,
            WorkingDirectory = Path.GetDirectoryName(webExe) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        // Force Kestrel to listen on the loopback-only port we picked
        // (overrides launchSettings.json / appsettings.json).
        psi.ArgumentList.Add($"--urls=http://127.0.0.1:{port}");
        // Mirror an env override so any code reading the env directly
        // sees the same value. ASP.NET Core honours both.
        psi.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        // Tell the child it's running embedded so future code can
        // hide browser-only chrome (e.g. external-link icons) if we
        // ever want shell-aware behaviour. Read via Environment.GetEnvironmentVariable.
        psi.EnvironmentVariables["DAFTARX_SHELL"] = "desktop";

        var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start EgyptTax.Web.exe");
        return p;
    }

    /// <summary>Polls a TCP connect every ~150ms until either it
    /// succeeds (Kestrel is listening) or the timeout elapses.</summary>
    private static bool WaitForPort(string host, int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);
                if (task.Wait(500) && client.Connected) return true;
            }
            catch { /* not ready yet */ }
            Thread.Sleep(150);
        }
        return false;
    }

    private static void SafeKill(Process? p)
    {
        if (p is null) return;
        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(2000);
            }
        }
        catch { /* best-effort */ }
        finally { p.Dispose(); }
    }

    private static void ShowFatal(string message)
    {
        try
        {
            new PhotinoWindow()
                .SetTitle("DaftarX — startup error")
                .SetUseOsDefaultLocation(false)
                .SetSize(640, 360)
                .Center()
                .Load(BuildDataUrl(message))
                .WaitForClose();
        }
        catch
        {
            // If Photino itself can't open (rare; usually missing
            // WebView2 runtime), surface via the OS message box.
            Console.Error.WriteLine(message);
        }
    }

    private static Uri BuildDataUrl(string message)
    {
        var html =
            "<!doctype html><html><body style=\"font-family:Segoe UI,sans-serif;padding:24px;line-height:1.5;\">"
            + "<h2 style=\"margin-top:0;\">DaftarX could not start</h2>"
            + "<pre style=\"white-space:pre-wrap;background:#f4f4f4;padding:12px;border-radius:6px;\">"
            + System.Net.WebUtility.HtmlEncode(message)
            + "</pre></body></html>";
        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
        return new Uri("data:text/html;base64," + b64);
    }
}
