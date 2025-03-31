using System;
using System.Collections.Generic;
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
    /// StartupWindow.xaml 的互動邏輯
    /// </summary>
    public partial class StartupWindow : Window
    {
        public StartupWindow()
        {
            InitializeComponent();
        }

        private void BtnFileSearch_OnClick(object sender, RoutedEventArgs e)
        {
            Hide();
            FileSearchDemo mainWindow = new FileSearchDemo();
            mainWindow.ShowDialog();
            Show();
        }

        private void BtnReport_OnClick(object sender, RoutedEventArgs e)
        {
            Hide();
            MainWindow mainWindow = new MainWindow();
            mainWindow.ShowDialog();
            Show();
        }
    }
}
