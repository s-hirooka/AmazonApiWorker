namespace AmazonApiWorker;

public partial class MainForm : Form
{
    private readonly AmazonAffiliateQueueRepository _repository;
    private readonly CancellationTokenSource _cts = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private bool _processing;
    private DateTime _nextResultJsonCleanupUtc = DateTime.MinValue;

    public MainForm()
    {
        InitializeComponent();
        ApplyAppIcon();

        _repository = new AmazonAffiliateQueueRepository(AmazonAffiliateQueueRepository.GetDefaultDbPath());
        // 画面のタイトルとタスクトレイの表示にバージョンを出す（どの版が動いているか分かるように）
        string version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "";
        string title = string.IsNullOrEmpty(version) ? Text : $"{Text} Ver. {version}";
        Text = title;

        notifyIcon1.Visible = true;
        notifyIcon1.Text = title;

        _timer.Interval = 1000;
        _timer.Tick += async (_, _) => await ProcessNextAsync();
    }

    private void ApplyAppIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AmazonApiWorker.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        var icon = new Icon(iconPath);
        Icon = icon;
        notifyIcon1.Icon = icon;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AmazonWorkerProcess.RegisterWindowHandle(Handle);
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

    private async Task ProcessNextAsync()
    {
        if (_processing)
        {
            return;
        }

        var item = _repository.TryTakeNextPending();
        if (item == null)
        {
            CleanupOldResultJsonIfDue();
            return;
        }

        _processing = true;
        try
        {
            AddLog($"処理開始: {item.Mode} ASIN={item.Asin} URL={item.ProductUrl}");
            if (!item.Mode.Equals("CreatorsApi", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("WebView2 mode is disabled. Use CreatorsApi mode.");
            }

            var (resultUrl, resultJson) = await CreateByCreatorsApiAsync(item);
            if (string.IsNullOrWhiteSpace(resultUrl) || !resultUrl.Contains("http", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("アフィリリンクを取得できませんでした。");
            }

            _repository.MarkSuccess(item.Id, resultUrl, resultJson);
            AddLog($"成功: {resultUrl}" + (string.IsNullOrEmpty(resultJson) ? "（商品情報なし）" : ""));
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

    // 待ち行列が空のときに、10分に1回だけ古い商品情報（JSON）を消す。
    // twi-autoV2 は結果を数秒以内に読み取るので、1時間残せば十分。
    private void CleanupOldResultJsonIfDue()
    {
        if (DateTime.UtcNow < _nextResultJsonCleanupUtc)
        {
            return;
        }

        _nextResultJsonCleanupUtc = DateTime.UtcNow.AddMinutes(10);
        try
        {
            _repository.ClearOldResultJson(TimeSpan.FromHours(1));
        }
        catch (Exception ex)
        {
            AddLog("古い商品情報の削除に失敗: " + ex.Message);
        }
    }

    private static async Task<(string Url, string ItemJson)> CreateByCreatorsApiAsync(AmazonAffiliateQueueItem item)
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

        return (result.Url, result.ItemJson);
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
