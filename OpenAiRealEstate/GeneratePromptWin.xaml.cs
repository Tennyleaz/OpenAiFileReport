using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OpenAiFileReport
{
    /// <summary>
    /// GeneratePromptWin.xaml 的互動邏輯
    /// </summary>
    public partial class GeneratePromptWin : Window
    {
        public string Prompt { get; private set; }

        public GeneratePromptWin(string prompt)
        {
            InitializeComponent();
            Prompt = tbPrompt.Text = prompt;
        }

        private void BtnGo_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GeneratePromptWin_OnClosing(object sender, CancelEventArgs e)
        {
            Prompt = tbPrompt.Text;
        }
    }
}
