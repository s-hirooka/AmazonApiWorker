using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AmazonApiWorker;

public static class AmazonWorkerProcess
{
    private static readonly string SignalFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "twi-autoV2",
        "amazon-worker-window.txt");

    public static void RegisterWindowHandle(IntPtr handle)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SignalFilePath)!);
        File.WriteAllText(SignalFilePath, handle.ToInt64().ToString());
    }

    public static void ShowExistingWindow()
    {
        try
        {
            if (!File.Exists(SignalFilePath))
            {
                return;
            }

            string text = File.ReadAllText(SignalFilePath);
            if (!long.TryParse(text, out long value))
            {
                return;
            }

            IntPtr handle = new(value);
            ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
        }
        catch
        {
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;
}
