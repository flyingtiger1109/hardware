using System;
using System.Windows.Forms;
using HZCYKJTHardWare.CSharpDemo.Native;

namespace HZCYKJTHardWare.CSharpDemo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            DpiAwareness.Enable();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
