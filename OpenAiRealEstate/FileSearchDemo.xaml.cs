using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AssemblyAI;
using Microsoft.Win32;
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
        private readonly string tempFolder;  // %localappdata%/OpenAiTempFiles
        private IndexClient indexClient;
        private Pinecone.Index myIndex;
        internal ObservableCollection<InputFileModel> InputFiles = new ObservableCollection<InputFileModel>();

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
            tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAiTempFiles");

            try
            {
                _openAiKey = File.ReadAllText("openai_key.txt").Trim();
                _pinecodeApiKey = File.ReadAllText("pinecone_key.txt").Trim();
                openAiClient = new OpenAIClient(new OpenAIAuthentication(_openAiKey));
                pineconeClient = new PineconeClient(_pinecodeApiKey);

                if (!Directory.Exists(tempFolder))
                    Directory.CreateDirectory(tempFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Faild to read keys from txt!\n" + ex.Message);
                Loaded -= FileSearchDemo_OnLoaded;
            }            
        }

        private async void FileSearchDemo_OnLoaded(object sender, RoutedEventArgs e)
        {
            lbYourName.Content = "You: " + indexName;
            LoadFileList();
            inputFileListBox.ItemsSource = InputFiles;

            // create index if not exists
            IndexList indexes = await pineconeClient.ListIndexesAsync();
            myIndex = indexes.Indexes?.FirstOrDefault(x => x.Name == "n8n-wct-manual");
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

            //// list files in the index
            //indexClient = pineconeClient.Index(host: myIndex.Host);
            //var listResponse = await indexClient.ListAsync(new ListRequest
            //{
            //    Limit = 100,
            //});
            //lbYourFiles.Content = "Your Files: " + listResponse.Vectors.Count();

            //// find file names from vectors
            //HashSet<string> fileNames = new HashSet<string>();
            //foreach (var vector in listResponse.Vectors)
            //{
            //    int cutIdx = vector.Id.IndexOf('#');
            //    if (cutIdx >= 0)
            //    {
            //        string name = vector.Id.Substring(0, cutIdx);
            //        fileNames.Add(name);
            //    }
            //}
        }

        private void BtnAddFile_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Text Files (*.txt)|*.txt|PDF Files (*.pdf)|*.pdf";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dialog.Title = "Select your file:";
            if (dialog.ShowDialog() != true)
                return;

            FileInfo fileInfo = new FileInfo(dialog.FileName);
            if (fileInfo.Exists)
            {
                // copy to temp folder
                File.Copy(fileInfo.FullName, Path.Combine(tempFolder, fileInfo.Name), true);

                // add to list
                InputFileModel newFile = new InputFileModel()
                {
                    FullPath = fileInfo.FullName,
                    FileType = fileInfo.Extension,
                    FileName = fileInfo.Name,
                };
                if (fileInfo.Extension == ".txt")
                {
                    newFile.Content = File.ReadAllText(fileInfo.FullName).Trim();
                    newFile.IsProcessed = true;
                    newFile.IsSelected = true;
                }                
                InputFiles.Add(newFile);

                // upload to pinecone for bigger files
                if (fileInfo.Extension == ".pdf")
                {

                }
            }
        }

        private void FileSearchDemo_OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveFileList();
        }

        private void SaveFileList()
        {
            string jsonName = Path.Combine(tempFolder, "FileList.json");
            string jsonString = JsonSerializer.Serialize(InputFiles);
            File.WriteAllText(jsonName, jsonString);
        }

        private void LoadFileList()
        {
            string jsonName = Path.Combine(tempFolder, "FileList.json");
            if (File.Exists(jsonName))
            {
                string jsonString = File.ReadAllText(jsonName);
                InputFiles = JsonSerializer.Deserialize<ObservableCollection<InputFileModel>>(jsonString);
            }
        }
    }
}
