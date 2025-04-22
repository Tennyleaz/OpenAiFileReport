using System;
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
using System.Windows.Shapes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace OpenAiFileReport
{
    /// <summary>
    /// QueryResultWindow.xaml 的互動邏輯
    /// </summary>
    public partial class QueryResultWindow : Window
    {
        public QueryResultWindow(List<string> queryText)
        {
            InitializeComponent();

            for (int i = 0; i < queryText.Count; i++)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"Result {i}",
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                panel.Children.Add(new TextBlock
                {
                    Text = queryText[i],
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }
    }
}
