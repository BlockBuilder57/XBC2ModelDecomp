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

namespace XBC2ModelDecomp
{
    /// <summary>
    /// Interaction logic for MainFormTest.xaml
    /// </summary>
    public partial class MainFormTest : Window
    {
        public MainFormTest()
        {
            InitializeComponent();
            App.LogEvent += LogEvent;

            string[] Quotes =
            {
                "Find me on GitHub!",
                "\"Humongous hungolomghnonolougongus.\"",
                "\"Do you wish to change it? The future?\"",
                "\"Oops! That wasn't supposed to happen...\"",
                "\"I like your attitude!\"",
                "Very Funny Quote Goes Here"
            };
            txtConsole.Text = Quotes[new Random().Next(0, Quotes.Length - 1)];
        }

        private void LogEvent(object message)
        {
            Dispatcher.Invoke(() =>
            {
                txtConsole.AppendText(string.IsNullOrWhiteSpace(txtConsole.Text) ? message.ToString() : '\n' + message.ToString());
                txtConsole.ScrollToEnd();
            });
        }

        private void SelectFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Model Files (*.wimdo)|*.wimdo|Map Files (*.wismda)|*.wismda|All files (*.*)|*.*", Multiselect = true };
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                App.FilePaths = ofd.FileNames;
                App.OutputPaths = new string[App.FilePaths.Length];
                for (int i = 0; i < App.FilePaths.Length; i++)
                    App.OutputPaths[i] = App.FilePaths[i].Remove(App.FilePaths[i].LastIndexOf('.'));

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
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (App.OutputPaths != null)
                fbd.SelectedPath = App.OutputPaths[0].Remove(App.OutputPaths[0].LastIndexOf('\\'));
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                for (int i = 0; i < App.OutputPaths.Length; i++)
                    App.OutputPaths[i] = fbd.SelectedPath + $@"\{Path.GetFileNameWithoutExtension(App.FilePaths[i])}";
                EXtxtOutput.Text = fbd.SelectedPath;
            }
        }

        private void EXsldLOD_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            EXsldLOD.Value = (int)EXsldLOD.Value;
        }

        private void ExtractFile(object sender, RoutedEventArgs e)
        {
            if (App.FilePaths == null || App.FilePaths.Length == 0)
                return;
            App.ExportTextures = EXcbxTextures.IsChecked.Value;
            App.ExportFlexes = EXcbxFlexes.IsChecked.Value;
            App.ExportAnims = EXcbxAnims.IsChecked.Value;
            App.ExportOutlines = EXcbxOutlines.IsChecked.Value;
            App.SaveRawFiles = EXcbxRawFiles.IsChecked.Value;
            App.LOD = (int)EXsldLOD.Value;

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
                            new ModelTools();
                            break;
                        case ".wismda":
                            new MapTools();
                            break;
                    }
                }
            });
            taskThread.Start();
        }
    }
}
