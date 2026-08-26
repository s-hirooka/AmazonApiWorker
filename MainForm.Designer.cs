namespace AmazonApiWorker;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
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
        lstLog = new ListBox();
        notifyIcon1 = new NotifyIcon(components);
        notifyMenu = new ContextMenuStrip(components);
        showToolStripMenuItem = new ToolStripMenuItem();
        exitToolStripMenuItem = new ToolStripMenuItem();
        notifyMenu.SuspendLayout();
        SuspendLayout();
        // 
        // lstLog
        // 
        lstLog.Dock = DockStyle.Fill;
        lstLog.FormattingEnabled = true;
        lstLog.HorizontalScrollbar = true;
        lstLog.ItemHeight = 15;
        lstLog.Location = new Point(0, 0);
        lstLog.Name = "lstLog";
        lstLog.Size = new Size(900, 500);
        lstLog.TabIndex = 0;
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
        notifyMenu.Size = new Size(104, 48);
        // 
        // showToolStripMenuItem
        // 
        showToolStripMenuItem.Name = "showToolStripMenuItem";
        showToolStripMenuItem.Size = new Size(103, 22);
        showToolStripMenuItem.Text = "表示";
        showToolStripMenuItem.Click += showToolStripMenuItem_Click;
        // 
        // exitToolStripMenuItem
        // 
        exitToolStripMenuItem.Name = "exitToolStripMenuItem";
        exitToolStripMenuItem.Size = new Size(103, 22);
        exitToolStripMenuItem.Text = "終了";
        exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 500);
        Controls.Add(lstLog);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Amazon Affiliate Worker";
        notifyMenu.ResumeLayout(false);
        ResumeLayout(false);
    }
}
