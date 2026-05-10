namespace AmazonApiWorker;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private SplitContainer splitContainer1 = null!;
    private Microsoft.Web.WebView2.WinForms.WebView2 webView = null!;
    private Panel rightPanel = null!;
    private Panel popupPanel = null!;
    private ListBox lstLog = null!;
    private NotifyIcon notifyIcon1 = null!;
    private ContextMenuStrip notifyMenu = null!;
    private ToolStripMenuItem showToolStripMenuItem = null!;
    private ToolStripMenuItem exitToolStripMenuItem = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        splitContainer1 = new SplitContainer();
        webView = new Microsoft.Web.WebView2.WinForms.WebView2();
        rightPanel = new Panel();
        lstLog = new ListBox();
        popupPanel = new Panel();
        notifyIcon1 = new NotifyIcon(components);
        notifyMenu = new ContextMenuStrip(components);
        showToolStripMenuItem = new ToolStripMenuItem();
        exitToolStripMenuItem = new ToolStripMenuItem();
        ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
        splitContainer1.Panel1.SuspendLayout();
        splitContainer1.Panel2.SuspendLayout();
        splitContainer1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)webView).BeginInit();
        rightPanel.SuspendLayout();
        notifyMenu.SuspendLayout();
        SuspendLayout();
        // 
        // splitContainer1
        // 
        splitContainer1.Dock = DockStyle.Fill;
        splitContainer1.Location = new Point(0, 0);
        splitContainer1.Name = "splitContainer1";
        splitContainer1.Panel1.Controls.Add(webView);
        splitContainer1.Panel2.Controls.Add(rightPanel);
        splitContainer1.Size = new Size(1280, 760);
        splitContainer1.SplitterDistance = 850;
        splitContainer1.TabIndex = 0;
        // 
        // webView
        // 
        webView.AllowExternalDrop = true;
        webView.CreationProperties = null;
        webView.DefaultBackgroundColor = Color.White;
        webView.Dock = DockStyle.Fill;
        webView.Location = new Point(0, 0);
        webView.Name = "webView";
        webView.Size = new Size(850, 760);
        webView.TabIndex = 0;
        webView.ZoomFactor = 1D;
        // 
        // rightPanel
        // 
        rightPanel.Controls.Add(lstLog);
        rightPanel.Controls.Add(popupPanel);
        rightPanel.Dock = DockStyle.Fill;
        rightPanel.Location = new Point(0, 0);
        rightPanel.Name = "rightPanel";
        rightPanel.Size = new Size(426, 760);
        rightPanel.TabIndex = 0;
        // 
        // lstLog
        // 
        lstLog.Dock = DockStyle.Fill;
        lstLog.FormattingEnabled = true;
        lstLog.HorizontalScrollbar = true;
        lstLog.ItemHeight = 15;
        lstLog.Location = new Point(0, 320);
        lstLog.Name = "lstLog";
        lstLog.Size = new Size(426, 440);
        lstLog.TabIndex = 1;
        // 
        // popupPanel
        // 
        popupPanel.Dock = DockStyle.Top;
        popupPanel.Location = new Point(0, 0);
        popupPanel.Name = "popupPanel";
        popupPanel.Size = new Size(426, 320);
        popupPanel.TabIndex = 0;
        popupPanel.Visible = false;
        // 
        // notifyIcon1
        // 
        notifyIcon1.ContextMenuStrip = notifyMenu;
        notifyIcon1.Icon = SystemIcons.Application;
        notifyIcon1.Text = "Amazon Affiliate Worker";
        notifyIcon1.DoubleClick += notifyIcon1_DoubleClick;
        // 
        // notifyMenu
        // 
        notifyMenu.Items.AddRange(new ToolStripItem[] { showToolStripMenuItem, exitToolStripMenuItem });
        notifyMenu.Name = "notifyMenu";
        notifyMenu.Size = new Size(99, 48);
        // 
        // showToolStripMenuItem
        // 
        showToolStripMenuItem.Name = "showToolStripMenuItem";
        showToolStripMenuItem.Size = new Size(98, 22);
        showToolStripMenuItem.Text = "表示";
        showToolStripMenuItem.Click += showToolStripMenuItem_Click;
        // 
        // exitToolStripMenuItem
        // 
        exitToolStripMenuItem.Name = "exitToolStripMenuItem";
        exitToolStripMenuItem.Size = new Size(98, 22);
        exitToolStripMenuItem.Text = "終了";
        exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 760);
        Controls.Add(splitContainer1);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Amazon Affiliate Worker";
        splitContainer1.Panel1.ResumeLayout(false);
        splitContainer1.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
        splitContainer1.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)webView).EndInit();
        rightPanel.ResumeLayout(false);
        notifyMenu.ResumeLayout(false);
        ResumeLayout(false);
    }
}
