using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Threading;
using System.Reflection;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Globalization;

namespace XBC2ModelDecomp
{
    /// <summary>
    /// Interaction logic for MainFormTest.xaml
    /// </summary>
    public partial class MainFormTest : Window
    {
        public static FormatTools FormatTools = new FormatTools();
        public static ModelTools ModelTools = new ModelTools();
        public static MapTools MapTools = new MapTools();
        public static TextureTools TextureTools = new TextureTools();

        public string[] Quotes =
        {
            "Find me on GitHub!",
            "\"Humongous hungolomghnonolougongus.\"",
            "\"Do you wish to change it? The future?\"",
            "\"Oops! That wasn't supposed to happen...\"",
            "\"I like your attitude!\"",
            "Very Funny Quote™ Goes Here",
            "\"i'm very bad at this\" - Block, 2019"
        };

        public MainFormTest()
        {
            InitializeComponent();
            App.LogEvent += LogEvent;

            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            
            txtConsole.Text = Quotes[new Random().Next(0, Quotes.Length)];

            if (FormatTools == null)
                FormatTools = new FormatTools();
            if (ModelTools == null)
                ModelTools = new ModelTools();
            if (MapTools == null)
                MapTools = new MapTools();
            if (TextureTools == null)
                TextureTools = new TextureTools();

            this.Title = $"XBC2ModelDecomp v{Assembly.GetEntryAssembly().GetName().Version.ToString(2)}-{ThisAssembly.Git.Commit}";
        }

        private void LogEvent(object message)
        {
            Dispatcher.Invoke(() =>
            {
                txtConsole.AppendText(string.IsNullOrWhiteSpace(txtConsole.Text) ? message.ToString() : '\n' + message.ToString());
                txtConsole.ScrollToEnd();
            });
        }

        private void TabControlChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tabConsole != null && tabConsole.IsSelected && txtConsole.LineCount <= 1)
                txtConsole.Text = Quotes[new Random().Next(0, Quotes.Length)];
        }

        private void SelectFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Model Files (*.wismt)|*.wismt|Map Files (*.wismda)|*.wismda|Font Files (*.wifnt)|*.wifnt|All files (*.*)|*.*", Multiselect = true };
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                App.FilePaths = ofd.FileNames;
                App.OutputPaths = new string[App.FilePaths.Length];
                for (int i = 0; i < App.FilePaths.Length; i++)
                    App.OutputPaths[i] = App.FilePaths[i].Remove(App.FilePaths[i].LastIndexOf('.'));

                EXbtnOutput.IsEnabled = true;
                EXbtnExtract.IsEnabled = true;

                EXtxtOutput.Text = string.Join(", ", App.OutputPaths);
                EXtxtInput.Text = string.Join(", ", App.FilePaths);

                EXtxtOutput.CaretIndex = EXtxtOutput.Text.Length;
                EXtxtOutput.ScrollToHorizontalOffset(EXtxtOutput.GetRectFromCharacterIndex(EXtxtOutput.CaretIndex).Right);
                EXtxtInput.CaretIndex = EXtxtInput.Text.Length;
                EXtxtInput.ScrollToHorizontalOffset(EXtxtInput.GetRectFromCharacterIndex(EXtxtInput.CaretIndex).Right);
            }
        }

        private void SelectOutputDir(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog fbd = new CommonOpenFileDialog { IsFolderPicker = true };
            if (App.OutputPaths != null)
                fbd.InitialDirectory = App.OutputPaths[0].Remove(App.OutputPaths[0].LastIndexOf('\\'));
            if (fbd.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (App.OutputPaths != null)
                    for (int i = 0; i < App.OutputPaths.Length; i++)
                        App.OutputPaths[i] = fbd.FileName + $@"\{Path.GetFileNameWithoutExtension(App.FilePaths[i])}";
                EXtxtOutput.Text = fbd.FileName;
            }
        }

        private void ExtractFile(object sender, RoutedEventArgs e)
        {
            if (App.FilePaths == null || App.FilePaths.Length == 0)
                return;
            App.ExportTextures = EXcbxTextures.IsChecked.Value;
            App.ExportFlexes = EXcbxFlexes.IsChecked.Value;
            App.ExportAnims = EXcbxAnims.IsChecked.Value;
            App.ExportOutlines = EXcbxOutlines.IsChecked.Value;
            App.ExportMapMesh = EXcbxMapMesh.IsChecked.Value;
            App.ExportMapProps = EXcbxMapProps.IsChecked.Value;
            App.ShowInfo = EXcbxShowInfo.IsChecked.Value;
            App.LOD = (int)EXsldLOD.Value;
            App.PropSplitCount = (int)EXsldPropSplit.Value;
            App.ExportFormat = (Structs.ExportFormat)EXdropFormat.SelectedIndex;

            tabConsole.Focus();
            txtConsole.Text = "";
            App.PushLog($"Extracting {App.FilePaths.Length} file(s)...");

            Thread taskThread = new Thread(() =>
            {
                for (int i = 0; i < App.FilePaths.Length; i++)
                {
                    App.FileIndex = i;
                    switch (Path.GetExtension(App.FilePaths[i]))
                    {
                        case ".wimdo":
                        case ".wismt":
                        case ".wiefp":
                        case ".arc":
                        case ".mot":
                            ModelTools.ExtractModels();
                            break;
                        case ".wismda":
                            MapTools.ExtractMaps();
                            break;
                        case ".wifnt":
                            TextureTools.ExtractTextures();
                            break;
                    }
                }
            });
            taskThread.Start();
        }
    }
}
