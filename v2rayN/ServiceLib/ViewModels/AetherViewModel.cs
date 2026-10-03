namespace ServiceLib.ViewModels;

public partial class AetherViewModel : MyReactiveObject
{
    public ReactiveCommand<RxVoid, RxVoid> ConnectCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> DisconnectCmd { get; }

    public string[] ProtocolOptions { get; } = ["auto", "masque", "wireguard", "gool"];
    public string[] ScanOptions { get; } = ["turbo", "balanced", "thorough", "stealth", "ironclad"];
    public string[] IpOptions { get; } = ["v4", "v6", "both"];
    public string[] NoizeMasqueOptions { get; } = ["firewall", "gfw", "off"];
    public string[] NoizeWgOptions { get; } = ["balanced", "aggressive", "light", "off"];
    public string[] ExtraOptions { get; } = ["none", "tor", "tor_reverse", "tor_only", "psiphon", "psiphon_reverse", "psiphon_only"];
    public string[] ExtraTransportOptions { get; } = ["none", "tor", "tor_reverse", "tor_only", "psiphon", "psiphon_reverse", "psiphon_only"];
    public string[] PsiphonModeOptions { get; } = ["auto", "cdn", "direct"];
    public string[] PsiphonRegionOptions { get; } = [
        "", "AE", "AR", "AT", "AU", "BE", "BG", "BR", "CA", "CH", "CL", "CO",
        "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GB", "GR", "HK", "HR",
        "HU", "IE", "IL", "IN", "IS", "IT", "JP", "KR", "LT", "LU", "LV", "MD",
        "MX", "MY", "NL", "NO", "NZ", "PH", "PL", "PT", "RO", "RS", "SE", "SG",
        "SK", "TH", "TR", "TW", "UA", "US", "VN", "ZA",
    ];

    [Reactive]
    public partial bool IsRunning { get; set; }

    [Reactive]
    public partial bool IsBusy { get; set; }

    [Reactive]
    public partial string StatusText { get; set; }

    [Reactive]
    public partial string EndpointsText { get; set; }

    [Reactive]
    public partial string Protocol { get; set; }

    [Reactive]
    public partial string ScanMode { get; set; }

    [Reactive]
    public partial string IpVersion { get; set; }

    [Reactive]
    public partial bool QuickReconnect { get; set; }

    [Reactive]
    public partial bool MasqueHttp2 { get; set; }

    [Reactive]
    public partial string MasqueNoize { get; set; }

    [Reactive]
    public partial string WgNoize { get; set; }

    [Reactive]
    public partial string BindAddress { get; set; }

    [Reactive]
    public partial string Dns { get; set; }

    [Reactive]
    public partial string Upstream { get; set; }

    [Reactive]
    public partial string ExtraTransport { get; set; }

    [Reactive]
    public partial bool TorBridges { get; set; }

    [Reactive]
    public partial string PsiphonRegion { get; set; }

    [Reactive]
    public partial string PsiphonMode { get; set; }

    [Reactive]
    public partial bool SysProxyOn { get; set; }

    public AetherViewModel()
    {
        var item = AppManager.Instance.Config.AetherItem ??= new();
        Protocol = item.Protocol;
        ScanMode = item.ScanMode;
        IpVersion = item.IpVersion;
        QuickReconnect = item.QuickReconnect;
        MasqueHttp2 = item.MasqueHttp2;
        MasqueNoize = item.MasqueNoize;
        WgNoize = item.WgNoize;
        BindAddress = item.BindAddress;
        Dns = item.Dns;
        Upstream = item.Upstream;
        ExtraTransport = item.ExtraTransport;
        TorBridges = item.TorBridges;
        PsiphonRegion = item.PsiphonRegion;
        PsiphonMode = item.PsiphonMode;
        StatusText = string.Empty;
        EndpointsText = string.Empty;

        ConnectCmd = ReactiveCommand.CreateFromTask(ConnectAsync);
        DisconnectCmd = ReactiveCommand.CreateFromTask(DisconnectAsync);

        this.WhenAnyValue(x => x.SysProxyOn)
            .Skip(1)
            .SubscribeAsync(async on => await ApplySysProxyAsync(on));
    }

    private AetherItem Snapshot()
    {
        return new AetherItem
        {
            Protocol = Protocol,
            ScanMode = ScanMode,
            IpVersion = IpVersion,
            QuickReconnect = QuickReconnect,
            MasqueHttp2 = MasqueHttp2,
            MasqueNoize = MasqueNoize,
            WgNoize = WgNoize,
            BindAddress = BindAddress,
            Dns = Dns,
            Upstream = Upstream,
            ExtraTransport = ExtraTransport,
            TorBridges = TorBridges,
            PsiphonRegion = PsiphonRegion,
            PsiphonMode = PsiphonMode,
        };
    }

    private async Task ConnectAsync()
    {
        if (IsBusy || IsRunning)
        {
            return;
        }
        IsBusy = true;
        StatusText = ResUI.AetherStarting;
        try
        {
            var item = Snapshot();
            AppManager.Instance.Config.AetherItem = item;
            await ConfigHandler.SaveConfig(AppManager.Instance.Config);

            var (ok, msg) = await AetherService.Instance.StartAsync(item, UpdateHandler);
            StatusText = msg;
            IsRunning = ok;
            EndpointsText = ok
                ? $"SOCKS5 {AetherService.SocksAddress(item)} · HTTP {AetherService.HttpAddress(item)}"
                : string.Empty;
            if (ok && SysProxyOn)
            {
                await ApplySysProxyAsync(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DisconnectAsync()
    {
        if (IsBusy)
        {
            return;
        }
        IsBusy = true;
        try
        {
            if (SysProxyOn)
            {
                SysProxyOn = false;
            }
            await AetherService.Instance.StopAsync();
            IsRunning = false;
            EndpointsText = string.Empty;
            StatusText = ResUI.AetherStopped;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplySysProxyAsync(bool on)
    {
        // The analyzer (CA1416) accepts this runtime guard in place of a
        // platform attribute, without pushing annotations onto callers.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            var item = AppManager.Instance.Config.AetherItem ?? new AetherItem();
            var http = AetherService.HttpAddress(item);
            if (on)
            {
                ProxySettingWindows.SetProxy(http, ProxyBypass(), 2);
            }
            else
            {
                ProxySettingWindows.UnsetProxy();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AetherViewModel", ex);
        }
        await Task.CompletedTask;
    }

    private static string ProxyBypass()
    {
        // Mirrors the bypass list used by the standalone Aether GUI: plain
        // hostnames plus every RFC 1918 range, so LAN addresses never enter
        // the tunnel.
        var parts = new List<string> { "<local>", "localhost", "127.*", "10.*", "192.168.*" };
        for (var i = 16; i <= 31; i++)
        {
            parts.Add($"172.{i}.*");
        }
        return string.Join(";", parts);
    }

    private async Task UpdateHandler(bool notify, string msg)
    {
        NoticeManager.Instance.SendMessage(msg);
        await Task.CompletedTask;
    }
}

