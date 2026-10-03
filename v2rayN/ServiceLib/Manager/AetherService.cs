namespace ServiceLib.Manager;

/// <summary>
/// Aether core (CluvexStudio/Aether) lifecycle, managed beside — never inside —
/// the Xray-family pipeline: the exe takes CLI flags (no JSON config), serves
/// SOCKS5 on --bind plus a native HTTP proxy on port+1, and logs to MsgView
/// through the shared update callback like every other core.
/// </summary>
public sealed class AetherService
{
    private static readonly Lazy<AetherService> _instance = new(() => new());
    public static AetherService Instance => _instance.Value;

    private ProcessService? _process;
    private const string _tag = "AetherService";

    public bool IsRunning => _process is { HasExited: false };

    public static string SocksAddress(Models.Configs.AetherItem item)
    {
        // Display form: an unspecified bind is real for the core but useless
        // to clients, so it maps to loopback (the --bind flag itself keeps
        // 0.0.0.0 for LAN sharing).
        var (host, port) = SplitHostPort(NormalizeBind(item.BindAddress));
        return $"{host}:{port}";
    }

    public static string HttpAddress(Models.Configs.AetherItem item)
    {
        var (host, port) = SplitHostPort(NormalizeBind(item.BindAddress));
        return $"{host}:{(port >= 65535 ? 1 : port + 1)}";
    }

    public string BuildArguments(Models.Configs.AetherItem item)
    {
        var args = new List<string>();

        switch ((item.Protocol ?? "auto").ToLowerInvariant())
        {
            case "masque": args.Add("--masque"); break;
            case "wireguard": case "wg": args.Add("--wg"); break;
            case "gool": args.Add("--gool"); break;
        }

        args.Add((item.ScanMode ?? "balanced").ToLowerInvariant() switch
        {
            "turbo" => "--turbo",
            "thorough" => "--thorough",
            "stealth" => "--stealth",
            "ironclad" => "--ironclad",
            _ => "--balanced",
        });

        args.Add((item.IpVersion ?? "v4").ToLowerInvariant() switch
        {
            "v6" => "-6",
            "both" => "--dual",
            _ => "-4",
        });

        args.Add(item.QuickReconnect ? "--quick-reconnect" : "--no-quick-reconnect");

        var masqueFamily = (item.Protocol ?? "auto").ToLowerInvariant() is "auto" or "masque";
        var noize = masqueFamily ? item.MasqueNoize : item.WgNoize;
        if (!noize.IsNullOrEmpty())
        {
            args.Add("--noize");
            args.Add(noize);
        }

        var bind = NormalizeBind(item.BindAddress);
        if (!bind.Equals("127.0.0.1:1819", StringComparison.OrdinalIgnoreCase)
            && TryParseEndpoint(bind, out _))
        {
            args.Add("--bind");
            args.Add(bind);
        }

        // Native HTTP proxy, always on, own port (SOCKS port + 1).
        args.Add("--http-proxy");
        args.Add(HttpAddress(item));

        if (!item.Dns.IsNullOrEmpty())
        {
            args.Add("--dns");
            args.Add(item.Dns.Trim());
        }

        if (!item.Upstream.IsNullOrEmpty())
        {
            args.Add("--upstream");
            args.Add(item.Upstream.Trim());
        }

        var extra = (item.ExtraTransport ?? "none").ToLowerInvariant();
        var extraFlag = extra switch
        {
            "tor" => "--tor",
            "tor_reverse" => "--tor-reverse",
            "tor_only" => "--tor-only",
            "psiphon" => "--psiphon",
            "psiphon_reverse" => "--psiphon-reverse",
            "psiphon_only" => "--psiphon-only",
            _ => null,
        };
        if (extraFlag != null)
        {
            args.Add(extraFlag);
        }

        if (item.TorBridges && extra.StartsWith("tor"))
        {
            args.Add("--tor-bridges");
        }

        if (extra.StartsWith("psiphon"))
        {
            var region = (item.PsiphonRegion ?? "").Trim().ToUpperInvariant();
            if (!region.IsNullOrEmpty())
            {
                args.Add("--psiphon-region");
                args.Add(region);
            }
            var mode = (item.PsiphonMode ?? "auto").ToLowerInvariant();
            if (mode is "cdn" or "direct")
            {
                args.Add("--psiphon-mode");
                args.Add(mode);
            }
        }

        return string.Join(' ', args);
    }

    public async Task<(bool ok, string msg)> StartAsync(Models.Configs.AetherItem item, Func<bool, string, Task> updateFunc)
    {
        await StopAsync();

        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.aether);
        var exe = CoreInfoManager.Instance.GetCoreExecFile(coreInfo, out var msg);
        if (exe.IsNullOrEmpty())
        {
            return (false, msg);
        }

        try
        {
            var env = new Dictionary<string, string?>
            {
                { "AETHER_MASQUE_HTTP2", item.MasqueHttp2 ? "1" : "0" },
            };
            _process = new Services.ProcessService(
                fileName: exe,
                arguments: BuildArguments(item),
                workingDirectory: Utils.GetBinConfigPath(),
                displayLog: true,
                redirectInput: false,
                environmentVars: env,
                updateFunc: updateFunc);
            await _process.StartAsync();
            await Task.Delay(100);
            if (_process.HasExited)
            {
                var m = "Aether exited immediately — see the log above.";
                _process.Dispose();
                _process = null;
                return (false, m);
            }
            return (true, $"Aether started: SOCKS {SocksAddress(item)}, HTTP {HttpAddress(item)}");
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            _process = null;
            return (false, ex.Message);
        }
    }

    public async Task StopAsync()
    {
        try
        {
            if (_process != null)
            {
                await _process.StopAsync();
                _process.Dispose();
                _process = null;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private static string NormalizeBind(string? bind)
    {
        bind = (bind ?? "").Trim();
        if (bind.IsNullOrEmpty())
        {
            return "127.0.0.1:1819";
        }
        // Accept "port" as shorthand for 127.0.0.1:port.
        if (int.TryParse(bind, out var p) && p is >= 1 and <= 65535)
        {
            return $"127.0.0.1:{p}";
        }
        return bind;
    }

    private static (string host, int port) SplitHostPort(string addr)
    {
        var idx = addr.LastIndexOf(':');
        if (idx > 0 && int.TryParse(addr[(idx + 1)..], out var port) && port is >= 1 and <= 65535)
        {
            var host = addr[..idx].Trim('[', ']');
            if (host.IsNullOrEmpty() || host == "0.0.0.0" || host == "::")
            {
                host = "127.0.0.1";
            }
            return (host, port);
        }
        return ("127.0.0.1", 1819);
    }

    private static bool TryParseEndpoint(string addr, out string normalized)
    {
        normalized = NormalizeBind(addr);
        var idx = normalized.LastIndexOf(':');
        return idx > 0 && int.TryParse(normalized[(idx + 1)..], out var port) && port is >= 1 and <= 65535;
    }
}
