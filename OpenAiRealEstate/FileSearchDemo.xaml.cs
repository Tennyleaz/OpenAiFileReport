using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using AssemblyAI;
using Microsoft.VisualBasic.ApplicationServices;
using OpenAI;
using Pinecone;

namespace OpenAiFileReport
{
    /// <summary>
    /// FileSearchDemo.xaml 的互動邏輯
    /// </summary>
    public partial class FileSearchDemo : Window
    {
        private readonly string _pinecodeApiKey, _openAiKey;
        private readonly Guid userId;
        private readonly string indexName;
        private readonly OpenAIClient openAiClient;
        private readonly PineconeClient pineconeClient;
        private IndexClient indexClient;
        private Pinecone.Index myIndex;

        public FileSearchDemo()
        {
            InitializeComponent();
            if (Properties.Settings.Default.UserId == Guid.Empty)
            {
                Properties.Settings.Default.UserId = Guid.NewGuid();
                Properties.Settings.Default.Save();
            }
            userId = Properties.Settings.Default.UserId;
            indexName = "user-" + userId;

            try
            {
                _openAiKey = File.ReadAllText("openai_key.txt").Trim();
                _pinecodeApiKey = File.ReadAllText("pinecone_key.txt").Trim();
                openAiClient = new OpenAIClient(new OpenAIAuthentication(_openAiKey));
                pineconeClient = new PineconeClient(_pinecodeApiKey);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Faild to read keys from txt!\n" + ex.Message);
            }
        }

        private async void FileSearchDemo_OnLoaded(object sender, RoutedEventArgs e)
        {
            IndexList indexes = await pineconeClient.ListIndexesAsync();
            myIndex = indexes.Indexes?.FirstOrDefault(x => x.Name == indexName);
            if (myIndex != null)
            {

            }
            else
            {
                CreateIndexRequest request = new CreateIndexRequest()
                {
                    Name = indexName,
                    Dimension = 1538,
                    Metric = CreateIndexRequestMetric.Cosine,
                    VectorType = VectorType.Dense,
                    Spec = new ServerlessIndexSpec
                    {
                        Serverless = new ServerlessSpec
                        {
                            Cloud = ServerlessSpecCloud.Aws,
                            Region = "us-east-1",
                        }
                    },
                    DeletionProtection = DeletionProtection.Disabled
                };
                myIndex = await pineconeClient.CreateIndexAsync(request);
            }

            // list files in the index
            indexClient = pineconeClient.Index(host: myIndex.Host);
            var listResponse = await indexClient.ListAsync(new ListRequest
            {
                Limit = 3,
            });
        }
    }
}
