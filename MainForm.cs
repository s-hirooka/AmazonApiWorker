using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;

namespace AmazonApiWorker;

public partial class MainForm : Form
{
    private readonly AmazonAffiliateQueueRepository _repository;
    private readonly CancellationTokenSource _cts = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private WebView2? _popupWebView;
    private WebView2? _activeLinkWebView;
    private bool _processing;

    public MainForm()
    {
        InitializeComponent();

        _repository = new AmazonAffiliateQueueRepository(AmazonAffiliateQueueRepository.GetDefaultDbPath());
        notifyIcon1.Visible = true;
        notifyIcon1.Text = "Amazon Affiliate Worker";

        _timer.Interval = 1000;
        _timer.Tick += async (_, _) => await ProcessNextAsync();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AmazonWorkerProcess.RegisterWindowHandle(Handle);
        await InitializeWebViewAsync();
        _timer.Start();
        AddLog("ワーカーを起動しました。");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            notifyIcon1.ShowBalloonTip(1500, "Amazon Affiliate Worker", "常駐を継続しています。", ToolTipIcon.Info);
            return;
        }

        _cts.Cancel();
        notifyIcon1.Visible = false;
        base.OnFormClosing(e);
    }

    private async Task InitializeWebViewAsync()
    {
        string userDataFolder = Path.Combine(AppContext.BaseDirectory, "WebView2Profile");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await webView.EnsureCoreWebView2Async(env);
        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        webView.CoreWebView2.Settings.IsStatusBarEnabled = true;
        webView.CoreWebView2.NewWindowRequested += WebView_NewWindowRequested;
        _activeLinkWebView = webView;
        webView.CoreWebView2.Navigate("https://www.amazon.co.jp/");
    }

    private async void WebView_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        CoreWebView2Deferral? deferral = null;
        try
        {
            deferral = e.GetDeferral();
            DisposePopupWebView();

            _popupWebView = new WebView2 { Dock = DockStyle.Fill };
            popupPanel.Controls.Clear();
            popupPanel.Controls.Add(_popupWebView);
            popupPanel.Visible = true;

            await _popupWebView.EnsureCoreWebView2Async(webView.CoreWebView2.Environment);
            e.NewWindow = _popupWebView.CoreWebView2;
            e.Handled = true;
            _activeLinkWebView = _popupWebView;
            AddLog("Amazonリンク作成の子ウィンドウを受け取りました。");
        }
        catch (Exception ex)
        {
            AddLog("子ウィンドウ受け取りエラー: " + ex.Message);
        }
        finally
        {
            deferral?.Complete();
        }
    }

    private async Task ProcessNextAsync()
    {
        if (_processing || webView.CoreWebView2 == null)
        {
            return;
        }

        var item = _repository.TryTakeNextPending();
        if (item == null)
        {
            return;
        }

        _processing = true;
        try
        {
            AddLog($"処理開始: {item.Mode} ASIN={item.Asin} URL={item.ProductUrl}");
            string resultUrl;
            if (item.Mode.Equals("CreatorsApi", StringComparison.OrdinalIgnoreCase))
            {
                resultUrl = await CreateByCreatorsApiAsync(item);
            }
            else
            {
                resultUrl = await CreateByWebViewAsync(item);
            }

            if (string.IsNullOrWhiteSpace(resultUrl) || !resultUrl.Contains("http", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("アフィリリンクを取得できませんでした。");
            }

            _repository.MarkSuccess(item.Id, resultUrl);
            AddLog($"成功: {resultUrl}");
        }
        catch (Exception ex)
        {
            _repository.MarkFailed(item.Id, ex.Message);
            AddLog("失敗: " + ex.Message);
        }
        finally
        {
            _processing = false;
        }
    }

    private static async Task<string> CreateByCreatorsApiAsync(AmazonAffiliateQueueItem item)
    {
        using var client = new AmazonCreatorsApiClient(
            item.CredentialId,
            item.CredentialSecret,
            item.Version,
            item.TrackingId,
            item.Marketplace);

        var result = await client.GetAffiliateLinkByAsinAsync(item.Asin, CancellationToken.None);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Error);
        }

        return result.Url;
    }

    private async Task<string> CreateByWebViewAsync(AmazonAffiliateQueueItem item)
    {
        DisposePopupWebView();
        _activeLinkWebView = webView;

        await NavigateAndWaitAsync(webView, item.ProductUrl, 45000);
        await Task.Delay(3000);

        bool clicked = await ClickAffiliateButtonAsync(webView);
        if (!clicked)
        {
            throw new InvalidOperationException("リンク作成ボタンをクリックできませんでした。");
        }

        await Task.Delay(5000);
        WebView2 linkView = _activeLinkWebView ?? webView;

        bool trackingSet = await SetTrackingAsync(linkView, item.AssociateId, item.TrackingId);
        if (!trackingSet && _popupWebView != null)
        {
            trackingSet = await SetTrackingAsync(_popupWebView, item.AssociateId, item.TrackingId);
            linkView = _popupWebView;
        }

        if (!trackingSet)
        {
            throw new InvalidOperationException("アソシエイトID/トラッキングIDを設定できませんでした。");
        }

        await Task.Delay(1500);
        string url = "";
        for (int i = 0; i < 4; i++)
        {
            bool generated = await ClickGenerateButtonAsync(linkView);
            if (!generated && _popupWebView != null && linkView != _popupWebView)
            {
                generated = await ClickGenerateButtonAsync(_popupWebView);
                linkView = _popupWebView;
            }

            if (!generated)
            {
                AddLog("リンク生成ボタンをクリックできませんでした。再試行します。");
            }

            url = await WaitForShortUrlAsync(linkView, 20000);
            if (url.Contains("http", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            await CloseErrorPopupAsync(linkView);
            await Task.Delay(3000);
        }

        return url;
    }

    private async Task NavigateAndWaitAsync(WebView2 view, string url, int timeoutMs)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            view.CoreWebView2.NavigationCompleted -= Handler;
            tcs.TrySetResult();
        }

        view.CoreWebView2.NavigationCompleted += Handler;
        view.CoreWebView2.Navigate(url);
        using var timeout = new CancellationTokenSource(timeoutMs);
        using (timeout.Token.Register(() => tcs.TrySetCanceled()))
        {
            try
            {
                await tcs.Task;
            }
            catch
            {
                view.CoreWebView2.NavigationCompleted -= Handler;
            }
        }
    }

    private static async Task<bool> ClickAffiliateButtonAsync(WebView2 view)
    {
        string script = @"
(function() {
    const candidates = Array.from(document.querySelectorAll('.a-button.a-button-primary, button, input[type=""button""], input[type=""submit""], .a-button-text'));
    const button = candidates.find(el => {
        const text = ((el.innerText || el.textContent || el.value || el.getAttribute('aria-label') || '') + '').trim();
        const rect = el.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return false;
        return text.includes('リンク作成') || text.includes('リンク生成') || text.includes('アソシエイト') || el.className.includes('a-button-primary');
    });
    if (button) { button.click(); return true; }
    return false;
})();";
        return await ExecuteBoolAsync(view, script);
    }

    private static async Task<bool> SetTrackingAsync(WebView2 view, string associateId, string trackingId)
    {
        string script = $@"
(function() {{
    function setDropdownValue(selectElement, targetValue) {{
        if (!selectElement || !targetValue) return false;
        let matched = false;
        for (const opt of selectElement.options) {{
            if (((opt.value || '').trim() === targetValue) || ((opt.text || '').trim() === targetValue)) {{
                selectElement.value = opt.value;
                matched = true;
                break;
            }}
        }}
        if (!matched) return false;
        selectElement.dispatchEvent(new Event('input', {{ bubbles: true }}));
        selectElement.dispatchEvent(new Event('change', {{ bubbles: true }}));
        const prompt = selectElement.closest('.a-dropdown-container')?.querySelector('.a-dropdown-prompt');
        if (prompt) prompt.innerText = targetValue;
        return true;
    }}

    const associateDropdown = document.querySelector('select#amzn-ss-store-id-dropdown-text')
        || document.querySelector('select[aria-label=""ストアID""]');
    const trackingDropdown = document.querySelector('select#amzn-ss-tracking-id-dropdown-text')
        || document.querySelector('select[aria-label=""問い合わせ番号(追跡番号)""]');
    return setDropdownValue(associateDropdown, '{EscapeJs(associateId)}') && setDropdownValue(trackingDropdown, '{EscapeJs(trackingId)}');
}})();";
        return await ExecuteBoolAsync(view, script);
    }

    private static async Task<bool> ClickGenerateButtonAsync(WebView2 view)
    {
        string script = @"
(function() {
    const directButton =
        document.querySelector('button#amzn-ss-get-link-btn-text-announce')
        || document.querySelector('#amzn-ss-get-link-btn-text button')
        || document.querySelector('#amzn-ss-get-link-btn-text');
    if (directButton) { directButton.click(); return true; }

    const candidates = Array.from(document.querySelectorAll('button, input[type=""button""], input[type=""submit""], .a-button, .a-button-input, .a-button-text'));
    const button = candidates.find(el => {
        const text = ((el.innerText || el.textContent || el.value || el.getAttribute('aria-label') || el.getAttribute('alt') || '') + '').trim();
        const id = el.id || '';
        const name = el.getAttribute('name') || '';
        const cls = el.className || '';
        const rect = el.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return false;
        if (id === 'amzn-ss-get-link-btn-text-announce' || id === 'amzn-ss-get-link-btn-text' || name === 'submit.text-link' || cls.includes('amzn-ss-get-link-btn')) return true;
        return text.includes('リンクを取得') || text.includes('リンク作成') || text.includes('リンク生成') || text.includes('取得する');
    });
    if (button) { button.click(); return true; }
    return false;
})();";
        return await ExecuteBoolAsync(view, script);
    }

    private static async Task<string> WaitForShortUrlAsync(WebView2 view, int timeoutMs)
    {
        string script = @"
(function() {
    const el = document.querySelector('.amzn-ss-text-shortlink-textarea')
        || document.querySelector('textarea[id*=""short""]')
        || document.querySelector('input[id*=""short""]');
    return el ? (el.value || '') : '';
})();";

        int elapsed = 0;
        while (elapsed < timeoutMs)
        {
            await Task.Delay(1000);
            elapsed += 1000;
            string raw = await view.CoreWebView2.ExecuteScriptAsync(script);
            string value = DecodeJsonString(raw);
            if (value.Contains("http", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return "";
    }

    private static async Task CloseErrorPopupAsync(WebView2 view)
    {
        string script = @"
(function() {
    const closeButton = document.querySelector('button[aria-label*=""閉じる""], button[aria-label*=""Close""], .a-button-close, .a-popover-close, [data-action=""a-popover-close""]');
    if (closeButton) { closeButton.click(); return true; }
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true }));
    return false;
})();";
        await view.CoreWebView2.ExecuteScriptAsync(script);
    }

    private static async Task<bool> ExecuteBoolAsync(WebView2 view, string script)
    {
        string raw = await view.CoreWebView2.ExecuteScriptAsync(script);
        return raw == "true";
    }

    private static string DecodeJsonString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? "";
        }
        catch
        {
            return raw.Trim('"');
        }
    }

    private static string EscapeJs(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private void DisposePopupWebView()
    {
        if (_popupWebView == null)
        {
            return;
        }

        popupPanel.Controls.Clear();
        _popupWebView.Dispose();
        _popupWebView = null;
        popupPanel.Visible = false;
        _activeLinkWebView = webView;
    }

    private void AddLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AddLog), message);
            return;
        }

        string line = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {message}";
        lstLog.Items.Insert(0, line);
        while (lstLog.Items.Count > 500)
        {
            lstLog.Items.RemoveAt(lstLog.Items.Count - 1);
        }
    }

    private void notifyIcon1_DoubleClick(object sender, EventArgs e)
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void showToolStripMenuItem_Click(object sender, EventArgs e)
    {
        notifyIcon1_DoubleClick(sender, e);
    }

    private void exitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _cts.Cancel();
        notifyIcon1.Visible = false;
        Application.Exit();
    }
}
