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
                "Humongous hungolomghnonolougongus.",
                "Do you wish to change it? The future?",
                "Oops! That wasn't supposed to happen...",
                "I like your attitude!",
                "Very Funny Quote Goes Here"
            };
            txtConsole.Text = Quotes[new Random().Next(0, Quotes.Length - 1)];
        }

        private void LogEvent(string message)
        {
            txtConsole.AppendText('\n' + message);
            txtConsole.ScrollToEnd();
        }

        public string[] FileNames;

        private void SelectFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Model Files|*.wimdo|Map Files|*.wismda", Multiselect = true };
            if (!string.IsNullOrWhiteSpace(txtInput.Text))
            {
                string DirectoryPath = new FileInfo(txtInput.Text).DirectoryName;
                if (File.Exists(DirectoryPath))
                    ofd.InitialDirectory = DirectoryPath;
            }
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileNames = ofd.FileNames;
                App.FileNames = ofd.FileNames;
                App.OutputPath = new FileInfo(ofd.FileNames[0]).DirectoryName + $@"\{Path.GetFileNameWithoutExtension(ofd.FileNames[0])}";
                txtOutput.Text = App.OutputPath;
                txtInput.Text = string.Join(", ", ofd.FileNames);
                txtOutput.CaretIndex = txtOutput.Text.Length;
                txtOutput.ScrollToHorizontalOffset(txtOutput.GetRectFromCharacterIndex(txtOutput.CaretIndex).Right);
                txtInput.CaretIndex = txtInput.Text.Length;
                txtInput.ScrollToHorizontalOffset(txtInput.GetRectFromCharacterIndex(txtInput.CaretIndex).Right);
            }
        }

        private void SelectOutputDir(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                App.OutputPath = fbd.SelectedPath + $@"\{Path.GetFileNameWithoutExtension(App.FileNames[0])}";
                txtOutput.Text = App.OutputPath;
            }
        }

        private void DecompileFile(object sender, RoutedEventArgs e)
        {
            App.SaveAllFiles = cbxAllFiles.IsChecked.Value;
            App.ExportFlexes = cbxFlexes.IsChecked.Value;
            foreach (string file in FileNames)
            {
                switch (Path.GetExtension(file))
                {
                    case ".wimdo":
                    case ".wismt":
                    case ".wiefp":
                    case ".arc":
                    case ".mot":
                        new ModelTools(file);
                        break;
                    case ".wismda":
                        new MapTools(file);
                        break;
                }
            }
        }
    }
}
