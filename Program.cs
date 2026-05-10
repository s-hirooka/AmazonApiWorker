using System;
using System.Windows.Forms;
using PaApiWorker;

namespace AmazonApiWorker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var single = new SingleInstanceGuard(@"Global\TwiAutoV2_AmazonAffiliateWorker");
        if (!single.IsFirstInstance)
        {
            AmazonWorkerProcess.ShowExistingWindow();
            return;
        }

        Application.Run(new MainForm());
    }
}
