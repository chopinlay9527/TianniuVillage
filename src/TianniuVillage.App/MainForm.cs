using Microsoft.Web.WebView2.Core;
using TianniuVillage.Core;

namespace TianniuVillage.App;

public partial class MainForm : Form
{
    private readonly GameManager _manager = new();
    private readonly SimulationHost _host;
    private readonly CommandProcessor _commands;
    private float _lastSpeed = 1f;

    public MainForm()
    {
        InitializeComponent();

        _host = new SimulationHost(_manager);
        _commands = new CommandProcessor(_manager, PostInit, _ => { });
        _host.UpdateReady += json => BeginInvoke(() =>
        {
            try { _webView.CoreWebView2.PostWebMessageAsJson(json); }
            catch { }
        });

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                float cur = _manager.Speed;
                SetSpeed(cur <= 0 ? _lastSpeed : 0f);
            }
            else if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D5)
                SetSpeed(e.KeyCode - Keys.D0);
        };

        BuildWebView();
        FormClosed += (_, _) => _host.Stop();
    }

    public void SetSpeed(float mult)
    {
        _manager.Speed = mult;
        if (mult > 0) _lastSpeed = mult;
    }

    private void PostInit(string json)
    {
        BeginInvoke(() =>
        {
            try { _webView.CoreWebView2.PostWebMessageAsJson(json); }
            catch { }
        });
        _host.Wake();
    }

    private async void BuildWebView()
    {
        try
        {
            string userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TianniuVillage", "WebView2");
            foreach (var dir in new[] { "Cache", "Code Cache", "GPUCache", "Service Worker" })
            {
                var p = Path.Combine(userData, dir);
                if (Directory.Exists(p)) { try { Directory.Delete(p, true); } catch { } }
            }
            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (e.HttpStatusCode != 200)
                    _commands.Handle("""{"type":"jserror","msg":"页面加载失败"}""");
            };
            _webView.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                try { _commands.Handle(e.TryGetWebMessageAsString()); }
                catch (Exception ex)
                {
                    _commands.Handle("""{"type":"jserror","msg":"cmd error"}""" );
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            };

            string wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "tianniu.local", wwwroot, CoreWebView2HostResourceAccessKind.Allow);
            _webView.CoreWebView2.Navigate($"https://tianniu.local/index.html?t={DateTime.Now.Ticks}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WebView2 初始化失败：{ex.Message}\n\n请确认已安装 Microsoft Edge WebView2 运行时。",
                "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _host.Start();
    }
}
