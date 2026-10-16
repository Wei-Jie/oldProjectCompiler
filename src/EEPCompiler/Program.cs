using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EEPCompiler
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;

        [STAThread]
        private static int Main(string[] args)
        {
            // 若無參數（使用者直接雙擊 eepc.exe）或明確指定 --gui，則隱藏 Console 視窗並啟動 GUI 桌面介面
            if (args == null || args.Length == 0 ||
                (args.Length == 1 && string.Equals(args[0], "--gui", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    IntPtr hConsole = GetConsoleWindow();
                    if (hConsole != IntPtr.Zero)
                    {
                        ShowWindow(hConsole, SW_HIDE);
                    }
                }
                catch { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }

            // 否則進入 CLI 命令列模式（專供 Claude Code / PowerShell / 批次腳本呼叫）
            return CliRunner.Run(args);
        }
    }
}
