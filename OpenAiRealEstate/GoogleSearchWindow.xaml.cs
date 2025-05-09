using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    /// GoogleSearchWindow.xaml 的互動邏輯
    /// </summary>
    public partial class GoogleSearchWindow : Window
    {
        public GoogleSearchWindow(string resultJson)
        {
            InitializeComponent();

            try
            {
                List<GoogleSearchResult> results = JsonSerializer.Deserialize<List<GoogleSearchResult>>(resultJson);
                for (int i = 0; i < results.Count; i++)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"Result {i}: {results[i].title}",
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    });
                    panel.Children.Add(new TextBlock
                    {
                        Text = results[i].snippet,
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GoogleSearchWindow() error: " + ex);
            }
        }
    }
}
