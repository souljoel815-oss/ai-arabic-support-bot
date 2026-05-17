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
            // CLIENT MODE detection (v5 — LAN client installer).
            // A "client" install ships only DaftarX.exe + Photino, no
            // bundled web/ folder. The launcher detects this and opens
            // a window pointed at the customer's main DaftarX server
            // instead of spawning a local Kestrel. First launch prompts
            // for the server URL and saves it to a per-user config so
            // subsequent launches go straight to the window.
            //
            // Three sources for the server URL, checked in order:
            //   1. --server <url> / --server=<url> on the command line
            //   2. DAFTARX_SERVER env var
            //   3. %LOCALAPPDATA%\DaftarX\server.txt (saved by the
            //      first-run prompt below)
            // If none of those resolve AND there's no bundled web exe
            // (i.e. this is a client install), prompt the user.
            var serverUrl = ResolveServerUrl(args);
            var bundledWebExists = File.Exists(
                Path.Combine(AppContext.BaseDirectory, "web", "EgyptTax.Web.exe"));

            if (serverUrl is null && !bundledWebExists)
            {
                serverUrl = PromptForServerUrl();
                if (string.IsNullOrWhiteSpace(serverUrl)) return 0;
                SaveServerUrl(serverUrl);
            }

            string loadUrl;
            Process? web = null;
            var attached = false;
            var clientMode = serverUrl is not null;

            if (clientMode)
            {
                loadUrl = serverUrl!;
            }
            else
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

                var connection = ResolveConnectionString(args, webExe);
                var port = ResolvePort(args);
                // Bind to 0.0.0.0 so the same Web instance is reachable
                // from other machines on the LAN (customer can hit
                // http://<server>:50063/ from a workstation while DaftarX
                // also renders it locally). Photino still loads via
                // 127.0.0.1 so the desktop window doesn't depend on the
                // machine's hostname being resolvable.
                var bindUrl = $"http://0.0.0.0:{port}/";
                loadUrl = $"http://127.0.0.1:{port}/";

                // Two sub-modes within server mode:
                //   (a) Service-attach — the DaftarX Windows service
                //       installed by the MSI is already serving on the
                //       port; we just open a window pointing at it and
                //       don't manage any child process. Window close =
                //       window close. The service keeps running for
                //       LAN workstations.
                //   (b) Standalone — no service is listening, so we
                //       spawn EgyptTax.Web.exe ourselves and tear it
                //       down on window close. This covers `dotnet run`
                //       dev launches and any customer who's disabled
                //       the service.
                attached = !IsLocalPortFree(port);
                if (!attached)
                {
                    web = StartWebProcess(webExe, bindUrl, connection);
                }

                if (!WaitForPort("127.0.0.1", port, TimeSpan.FromSeconds(20)))
                {
                    ShowFatal(attached
                        ? $"Found something on port {port} but it isn't answering. "
                          + $"Restart the DaftarX service or run EgyptTax.Web.exe directly to see the error."
                        : $"Web host did not start on port {port} within 20s.\n\n"
                          + "Run EgyptTax.Web.exe from a terminal to see the actual startup error.");
                    SafeKill(web);
                    return 3;
                }
            }

            try
            {
                var window = new PhotinoWindow
                {
                    // Hide DevTools + browser context menu so the
                    // bare http://127.0.0.1:port URL never shows up
                    // in the UI (no right-click "Inspect", no Ctrl+U
                    // "View Source"). End-users should perceive
                    // DaftarX as a desktop app, not a localhost site.
                    ContextMenuEnabled = false,
                    DevToolsEnabled = false,
                }
                    .SetTitle("DaftarX")
                    .SetUseOsDefaultLocation(false)
                    .SetSize(1400, 900)
                    .Center();
                // Title-bar icon. Best-effort — if the ico is missing
                // (rare; the .csproj copies it next to DaftarX.exe)
                // Photino silently falls back to its built-in icon,
                // so we don't fail the launch over a missing image.
                var iconPath = Path.Combine(AppContext.BaseDirectory, "daftarx.ico");
                if (File.Exists(iconPath))
                {
                    window = window.SetIconFile(iconPath);
                }
                window
                    .RegisterWindowClosingHandler((object sender, EventArgs e) =>
                    {
                        // Only tear down the child we spawned ourselves;
                        // when attached to the service, leave it alone
                        // so LAN workstations keep working.
                        if (!attached) SafeKill(web);
                        return false; // false = allow close
                    })
                    .Load(new Uri(loadUrl))
                    .WaitForClose();

                return 0;
            }
            finally
            {
                if (!attached) SafeKill(web);
            }
        }
        catch (Exception ex)
        {
            ShowFatal("DaftarX shell crashed:\n\n" + ex);
            return 1;
        }
    }

    /// <summary>Per-user config file for client-mode installs. We use
    /// LocalApplicationData (per-user) because a client install rarely
    /// has more than one human at the keyboard and per-user avoids
    /// needing admin privs on first launch. ProgramData would be more
    /// "machine-wide" but requires elevation to write.</summary>
    private static string ClientConfigPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DaftarX",
            "server.txt");

    private static string? ResolveServerUrl(string[] args)
    {
        // (1) --server <url> / --server=<url>
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--server" && i + 1 < args.Length)
                return NormaliseServerUrl(args[i + 1]);
            if (args[i].StartsWith("--server=", StringComparison.OrdinalIgnoreCase))
                return NormaliseServerUrl(args[i].Substring("--server=".Length));
        }
        // (2) DAFTARX_SERVER env var
        var env = Environment.GetEnvironmentVariable("DAFTARX_SERVER");
        if (!string.IsNullOrWhiteSpace(env)) return NormaliseServerUrl(env);
        // (3) Saved config from a previous first-run prompt.
        var cfg = ClientConfigPath();
        if (File.Exists(cfg))
        {
            try
            {
                var saved = File.ReadAllText(cfg).Trim();
                if (!string.IsNullOrWhiteSpace(saved)) return NormaliseServerUrl(saved);
            }
            catch { /* corrupt — fall through to prompt */ }
        }
        return null;
    }

    /// <summary>Tolerant of customer input. Accepts bare IP
    /// (<c>192.168.1.10</c>), host:port (<c>server:50063</c>), or a
    /// full URL. Always returns <c>http://host:port</c> — DaftarX
    /// doesn't run HTTPS on-prem.</summary>
    private static string NormaliseServerUrl(string raw)
    {
        var s = raw.Trim().TrimEnd('/');
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            s = "http://" + s;
        }
        // Append :50063 if no explicit port (UriBuilder.Port returns
        // -1 when the scheme's default applies, e.g. http://1.2.3.4).
        try
        {
            var ub = new UriBuilder(s);
            if (ub.Uri.IsDefaultPort
                && !System.Text.RegularExpressions.Regex.IsMatch(raw, @":\d+"))
            {
                ub.Port = DefaultPort;
            }
            return ub.Uri.ToString().TrimEnd('/');
        }
        catch
        {
            return s;
        }
    }

    private static void SaveServerUrl(string url)
    {
        try
        {
            var cfg = ClientConfigPath();
            var dir = Path.GetDirectoryName(cfg);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(cfg, url);
        }
        catch
        {
            // Non-fatal: customer will be reprompted next launch.
        }
    }

    /// <summary>First-run setup window for client installs. Bilingual
    /// (Ar/En) form asking for the server IP. JS posts the value back
    /// to the host via <c>window.external.sendMessage</c>; the host
    /// captures it, closes the window, and returns. Cancelling the
    /// window returns null and the launcher exits.</summary>
    private static string? PromptForServerUrl()
    {
        string? captured = null;
        var html =
            "<!doctype html><html dir='rtl' lang='ar'><head><meta charset='utf-8'>"
            + "<style>"
            + "body{font-family:Segoe UI,Tahoma,sans-serif;padding:24px;background:#f5f5f5;color:#222;}"
            + "h2{margin:0 0 8px;}"
            + "p{margin:0 0 16px;color:#555;line-height:1.6;}"
            + "input{width:100%;padding:10px;font-size:14px;box-sizing:border-box;border:1px solid #ccc;border-radius:4px;}"
            + "button{margin-top:14px;padding:10px 22px;font-size:14px;background:#0f766e;color:#fff;border:0;border-radius:4px;cursor:pointer;}"
            + "button:hover{background:#0e6b63;}"
            + ".hint{font-size:12px;color:#777;margin-top:8px;}"
            + ".err{color:#b91c1c;margin-top:10px;min-height:18px;}"
            + "</style></head><body>"
            + "<h2>إعداد DaftarX</h2>"
            + "<p>أدخل عنوان السيرفر الرئيسي (الجهاز اللي مسطّب عليه نسخة الـ Server):</p>"
            + "<input id='url' type='text' placeholder='192.168.1.10' autofocus />"
            + "<div class='hint'>مثال: <code>192.168.1.10</code> أو <code>http://server-pc:50063</code></div>"
            + "<button onclick='go()'>اتصال</button>"
            + "<div class='err' id='err'></div>"
            + "<script>"
            + "function go(){"
            + "  var v=document.getElementById('url').value.trim();"
            + "  if(!v){document.getElementById('err').textContent='من فضلك أدخل عنوان السيرفر.';return;}"
            + "  window.external.sendMessage(v);"
            + "}"
            + "document.getElementById('url').addEventListener('keydown',function(e){if(e.key==='Enter')go();});"
            + "</script>"
            + "</body></html>";

        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
        var dataUrl = new Uri("data:text/html;charset=utf-8;base64," + b64);

        PhotinoWindow? promptWindow = null;
        promptWindow = new PhotinoWindow
        {
            ContextMenuEnabled = false,
            DevToolsEnabled = false,
        }
            .SetTitle("DaftarX — إعداد")
            .SetUseOsDefaultLocation(false)
            .SetSize(500, 340)
            .Center();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "daftarx.ico");
        if (File.Exists(iconPath))
        {
            promptWindow = promptWindow.SetIconFile(iconPath);
        }
        promptWindow
            .RegisterWebMessageReceivedHandler((object? sender, string message) =>
            {
                captured = message;
                // Close from the message handler so WaitForClose returns.
                try { ((PhotinoWindow?)sender)?.Close(); } catch { /* best-effort */ }
            })
            .Load(dataUrl)
            .WaitForClose();

        return string.IsNullOrWhiteSpace(captured) ? null : NormaliseServerUrl(captured);
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

    /// <summary>v5 — DaftarX uses a fixed, well-known port so the
    /// customer's server has a stable URL their workstations can
    /// bookmark and their firewall has a single opening to manage.
    /// No random fallback — if this port is busy we error out
    /// loudly so the operator notices instead of getting silently
    /// shunted to a port nobody else can reach. Override at launch
    /// with <c>--port &lt;n&gt;</c> or <c>DAFTARX_PORT=&lt;n&gt;</c>.</summary>
    private const int DefaultPort = 50063;

    private static int ResolvePort(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var p1) && p1 is > 0 and < 65536)
            {
                return p1;
            }
            if (args[i].StartsWith("--port=", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i].AsSpan("--port=".Length), out var p2)
                && p2 is > 0 and < 65536)
            {
                return p2;
            }
        }
        var env = Environment.GetEnvironmentVariable("DAFTARX_PORT");
        if (!string.IsNullOrWhiteSpace(env)
            && int.TryParse(env, out var p3) && p3 is > 0 and < 65536)
        {
            return p3;
        }
        return DefaultPort;
    }

    /// <summary>Binds the candidate port to <c>0.0.0.0</c> briefly to
    /// confirm nothing else owns it. Uses the all-interfaces address
    /// (not loopback) because that's what the child Web will bind to;
    /// a free loopback port doesn't guarantee the same port is free
    /// on the public interface.</summary>
    private static bool IsLocalPortFree(int port)
    {
        try
        {
            var l = new TcpListener(IPAddress.Any, port);
            l.Start();
            l.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static Process StartWebProcess(string webExe, string bindUrl, string? connection)
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
        // Force Kestrel to listen on the bind URL we picked
        // (overrides launchSettings.json / appsettings.json).
        psi.ArgumentList.Add($"--urls={bindUrl}");
        // Mirror an env override so any code reading the env directly
        // sees the same value. ASP.NET Core honours both.
        psi.EnvironmentVariables["ASPNETCORE_URLS"] = bindUrl;
        // Tell the child it's running embedded so future code can
        // hide browser-only chrome (e.g. external-link icons) if we
        // ever want shell-aware behaviour. Read via Environment.GetEnvironmentVariable.
        psi.EnvironmentVariables["DAFTARX_SHELL"] = "desktop";
        // Point the child at the resolved connection string (see
        // ResolveConnectionString). Program.cs in Web reads
        // EGYPTTAX_CONNECTION before falling back to its bundled
        // SQLite default, so this is the supported override channel.
        if (!string.IsNullOrWhiteSpace(connection))
        {
            psi.EnvironmentVariables["EGYPTTAX_CONNECTION"] = connection;
        }

        var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start EgyptTax.Web.exe");
        return p;
    }

    /// <summary>Resolves which DB the spawned Web should talk to.
    /// Priority:
    ///   1. <c>--db &lt;path-or-conn&gt;</c> on the command line.
    ///   2. <c>EGYPTTAX_CONNECTION</c> already set in the parent env
    ///      (passes through naturally; we just return null so
    ///      <see cref="StartWebProcess"/> doesn't override it).
    ///   3. Auto-detect dev tree: if the launcher is running from
    ///      under <c>src/EgyptTax.Desktop/bin/</c> AND a sibling
    ///      <c>src/EgyptTax.Web/bin/{cfg}/net8.0/daftarx.db</c>
    ///      exists, use that — so the desktop window shares the same
    ///      data the running dev session is editing.
    ///   4. None — Web falls back to its bundled SQLite file next to
    ///      EgyptTax.Web.exe via <c>PortableDefaults</c>.
    /// </summary>
    private static string? ResolveConnectionString(string[] args, string webExe)
    {
        // (1) --db <value> or --db=<value>
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--db" && i + 1 < args.Length)
            {
                return NormaliseToConnectionString(args[i + 1]);
            }
            if (args[i].StartsWith("--db=", StringComparison.OrdinalIgnoreCase))
            {
                return NormaliseToConnectionString(args[i].Substring("--db=".Length));
            }
        }

        // (2) Parent env passes through; child inherits unless we override.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EGYPTTAX_CONNECTION")))
        {
            return null;
        }

        // (3) Dev-tree auto-detect. Launcher running from
        //     src/EgyptTax.Desktop/bin/... — walk up to src/ and look
        //     for the dev Web project's DB at
        //     src/EgyptTax.Web/bin/{Debug,Release}/net8.0/daftarx.db.
        //     We deliberately DON'T use webExe's directory here because
        //     ResolveWebExecutable prefers the bundled web/ next to
        //     DaftarX.exe, which has no DB. The dev DB is the one the
        //     operator's running localhost:5000/5050 sessions share.
        var devDb = FindDevDatabase();
        if (devDb is not null)
        {
            return $"Data Source={devDb}";
        }

        return null;
    }

    private static string? FindDevDatabase()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        // Walk up to 6 levels looking for the EgyptTax.Desktop root,
        // then jump to the sibling EgyptTax.Web/bin/{cfg}/net8.0/.
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            if (!string.Equals(dir.Name, "EgyptTax.Desktop", StringComparison.OrdinalIgnoreCase))
                continue;
            var src = dir.Parent;
            if (src is null) break;
            foreach (var cfg in new[] { "Debug", "Release" })
            {
                var candidate = Path.Combine(
                    src.FullName, "EgyptTax.Web", "bin", cfg, "net8.0", "daftarx.db");
                if (File.Exists(candidate)) return candidate;
            }
            break;
        }
        return null;
    }

    /// <summary>Accepts either a bare path (<c>C:\data\foo.db</c>) or
    /// an already-formed connection string (<c>Data Source=foo.db</c>
    /// or a SQL-Server-style one) and returns what should land in
    /// <c>EGYPTTAX_CONNECTION</c>. SQL Server strings always contain
    /// <c>Server=</c> / <c>Data Source=</c> with an <c>=</c>; a bare
    /// path won't, so the heuristic is robust enough for the desktop
    /// launcher use-case.</summary>
    private static string NormaliseToConnectionString(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Contains('=', StringComparison.Ordinal)) return trimmed;
        return $"Data Source={trimmed}";
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
