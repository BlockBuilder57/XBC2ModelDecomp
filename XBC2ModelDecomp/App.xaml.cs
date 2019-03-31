using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace XBC2ModelDecomp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string[] FileNames;
        public static string OutputPath;
        public static bool SaveAllFiles;
        public static bool ExportFlexes;

        public delegate void Log(string logMessage);
        public static event Log LogEvent;

        public static void PushLog(string logMessage)
        {
            Console.WriteLine(logMessage);
            LogEvent?.Invoke(logMessage);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            App app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
