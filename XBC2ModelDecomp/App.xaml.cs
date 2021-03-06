﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;

namespace XBC2ModelDecomp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string[] FilePaths;
        public static string[] OutputPaths;
        public static int FileIndex;

        public static string CurFile { get { return FilePaths[FileIndex]; } }
        public static string CurOutputPath { get { return OutputPaths[FileIndex]; } }
        public static string CurFilePath { get { return Path.GetDirectoryName(FilePaths[FileIndex]); } }
        public static string CurFileNameNoExt { get { return Path.GetFileNameWithoutExtension(FilePaths[FileIndex]); } }
        public static string CurFilePathAndName { get { return $@"{CurFilePath}\{CurFileNameNoExt}"; } }

        public static bool ExportTextures;
        public static bool ExportFlexes;
        public static bool ExportAnims;
        public static bool ExportOutlines;
        public static bool ExportMapMesh;
        public static bool ExportMapProps;
        public static bool ShowInfo;
        public static int LOD;
        public static int PropSplitCount;
        public static Structs.ExportFormat ExportFormat = Structs.ExportFormat.XNALara;

        public delegate void Log(object logMessage);
        public static event Log LogEvent;

        public static void PushLog(object logMessage)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Console.WriteLine(logMessage.ToString());
                LogEvent?.Invoke(logMessage.ToString());
            });
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
