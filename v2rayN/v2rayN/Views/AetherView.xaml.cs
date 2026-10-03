namespace v2rayN.Views;

public partial class AetherView
{
    public AetherView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.ConnectCmd, v => v.btnConnect).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DisconnectCmd, v => v.btnDisconnect).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.StatusText, v => v.txtStatus.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.EndpointsText, v => v.txtEndpoints.Text).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Protocol, v => v.cmbProtocol.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ScanMode, v => v.cmbScanMode.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IpVersion, v => v.cmbIpVersion.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.MasqueNoize, v => v.cmbMasqueNoize.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.WgNoize, v => v.cmbWgNoize.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ExtraTransport, v => v.cmbExtraTransport.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PsiphonMode, v => v.cmbPsiphonMode.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.BindAddress, v => v.txtBindAddress.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Upstream, v => v.txtUpstream.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Dns, v => v.txtDns.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PsiphonRegion, v => v.cmbPsiphonRegion.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.QuickReconnect, v => v.togQuickReconnect.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.MasqueHttp2, v => v.togMasqueHttp2.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.TorBridges, v => v.togTorBridges.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SysProxyOn, v => v.togSysProxy.IsChecked).DisposeWith(disposables);
        });

        cmbProtocol.ItemsSource = ViewModel?.ProtocolOptions;
        cmbScanMode.ItemsSource = ViewModel?.ScanOptions;
        cmbIpVersion.ItemsSource = ViewModel?.IpOptions;
        cmbMasqueNoize.ItemsSource = ViewModel?.NoizeMasqueOptions;
        cmbWgNoize.ItemsSource = ViewModel?.NoizeWgOptions;
        cmbExtraTransport.ItemsSource = ViewModel?.ExtraTransportOptions;
        cmbPsiphonRegion.ItemsSource = ViewModel?.PsiphonRegionOptions;
        cmbPsiphonMode.ItemsSource = ViewModel?.PsiphonModeOptions;
    }
}
