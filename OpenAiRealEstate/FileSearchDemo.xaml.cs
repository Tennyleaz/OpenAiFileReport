using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Pinecone;
using Vector = Pinecone.Vector;

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
        private const string EMBEDDING_MODEL = "text-embedding-ada-002";
        private const int DIMENTION = 1536;
        private const string NAMESPACE = "documents";

        public FileSearchDemo()
        {
            InitializeComponent();
            if (string.IsNullOrEmpty(Properties.Settings.Default.UserId) || Properties.Settings.Default.UserId == Guid.Empty.ToString())
            {
                Properties.Settings.Default.UserId = Guid.NewGuid().ToString();
                Properties.Settings.Default.Save();
            }
            indexName = "user-" + Properties.Settings.Default.UserId;
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
                IsEnabled = false;
            }            
        }

        private async void FileSearchDemo_OnLoaded(object sender, RoutedEventArgs e)
        {
            lbYourName.Content = "You: " + indexName;
            LoadFileList();
            inputFileListBox.ItemsSource = InputFiles;
            tbSystemPrompt.Text = "You are an expert assistant that helps users summarize and interpret retrieved text from document searches.";

            // create index if not exists
            IndexList indexes;
            try
            {
                indexes = await pineconeClient.ListIndexesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to get pinecone index: " + ex);
                return;
            }
            myIndex = indexes.Indexes?.FirstOrDefault(x => x.Name == indexName);
            if (myIndex != null)
            {
                tbLogs.Text = "Found your index: " + indexName;
            }
            else
            {
                CreateIndexRequest request = new CreateIndexRequest()
                {
                    Name = indexName,
                    Dimension = DIMENTION,
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

                // get again to ensure created
                indexes = await pineconeClient.ListIndexesAsync();
                myIndex = indexes.Indexes?.FirstOrDefault(x => x.Name == indexName);
                if (myIndex == null)
                {
                    MessageBox.Show("Failed to create index!");
                    return;
                }

                tbLogs.Text = "Created your index: " + indexName;
            }

            // list files in the index
            indexClient = pineconeClient.Index(host: myIndex.Host);
            tbLogs.Text += "\nHost: " + myIndex.Host;
            
            var r = await indexClient.DescribeIndexStatsAsync(new DescribeIndexStatsRequest());
            if (r.Namespaces != null)
            {
                var ns = r.Namespaces[NAMESPACE];
                if (ns != null)
                {
                    tbLogs.Text += "\nVectors: " + ns.VectorCount;
                }
            }
        }

        private async void BtnAddFile_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "PDF Files (*.pdf)|*.pdf|Text Files (*.txt)|*.txt";
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
                //if (fileInfo.Extension == ".txt")
                //{
                //    newFile.Content = File.ReadAllText(fileInfo.FullName).Trim();
                //    newFile.IsProcessed = true;
                //    //newFile.IsSelected = true;
                //}
                InputFiles.Add(newFile);

                // upload to pinecone for bigger files
                //if (fileInfo.Extension == ".pdf")
                {
                    IsEnabled = false;
                    string documentId = await ReadDocument(fileInfo);
                    newFile.PineconeId = documentId;
                    newFile.IsProcessed = true;
                    IsEnabled = true;
                }
            }
        }

        private async Task<string> ReadDocument(FileInfo fileInfo)
        {
            List<string> data;
            if (fileInfo.Extension == ".pdf")
            {
                tbLogs.Text = "Reading PDF file...";
                PDFParser parser = new PDFParser();
                data = parser.ExtractText(fileInfo.FullName);
            }
            else if (fileInfo.Extension == ".txt")
            {
                tbLogs.Text = "Reading TXT file...";
                TxtParser parser = new TxtParser();
                data = parser.ExtractText(fileInfo.FullName);
            }
            else
            {
                MessageBox.Show("Does not supoort this file: " + fileInfo.Extension);
                return null;
            }

            tbLogs.Text = "Generating embeddings using GPT...";
            List<float[]> embeddings = await GetImbeddings(data);
            
            // ensure count is same
            if (embeddings.Count != data.Count)
            {
                MessageBox.Show("Error in embeddings count!");
                return null;
            }

            tbLogs.Text = "Upload embeddings to pinecone...";
            string name = "File-" + Guid.NewGuid().ToString();
            try
            {
                await StoreEmbeddings(name, data, embeddings, fileInfo.Name);
                tbLogs.Text = "OK.";
                return name;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Embedding error: " + ex);
                tbLogs.Text = ex.ToString();
                return null;
            }
        }

        private async Task<List<float[]>> GetImbeddings(List<string> data)
        {
            EmbeddingsRequest request = new EmbeddingsRequest(data, EMBEDDING_MODEL);
            EmbeddingsResponse response = await openAiClient.EmbeddingsEndpoint.CreateEmbeddingAsync(request);
            // get the embeddings double array
            List<float[]> embeddings = new List<float[]>();
            foreach (Datum dt in response.Data)
            {
                float[] embedding = dt.Embedding.Select(x => (float)x).ToArray();
                embeddings.Add(embedding);
            }
            return embeddings;
        }

        private async Task StoreEmbeddings(string vectorId, List<string> data, List<float[]> embeddings, string filename)
        {
            List<Vector> vectors = new List<Vector>();
            for (int i = 0; i < embeddings.Count; i++)
            {
                vectors.Add(new Vector
                {
                    Id = vectorId + $"-page-{i + 1}",
                    Values = embeddings[i],
                    Metadata = new Metadata
                    {
                        ["pageNumber"] = i + 1,
                        ["fileName"] = filename,
                        ["text"] = data[i]
                    },
                });
            }

            UpsertRequest request = new UpsertRequest()
            {
                Vectors = vectors,
                Namespace = NAMESPACE,
            };
            UpsertResponse response = await indexClient.UpsertAsync(request);
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

        private async void BtnAsk_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbSystemPrompt.Text))
                return;
            IsEnabled = false;
            try
            {
                string retrievedText = await QueryEmbeddings(tbUserPrompt.Text);
                if (!string.IsNullOrEmpty(retrievedText))
                {
                    tbLogs.Text = "Asking GPT...";
                    string userPrompt = $"User query: {tbUserPrompt.Text}\n\nMatched text from vector store:\n{retrievedText}\n\nPlease provide a concise, well-structured response.";
                    ChatRequest chatRequest = new ChatRequest(
                        model: "gpt-4o-mini",
                        messages: new List<Message>()
                        {
                        new Message(Role.System, tbSystemPrompt.Text),
                        new Message(Role.User, userPrompt)
                        }
                    //responseFormat: ChatResponseFormat.JsonSchema,
                    //jsonSchema: new JsonSchema("report_schema", schema)
                    );
                    ChatResponse chatResponse = await openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                    tbOutput.Text = chatResponse.Choices.First().ToString();
                    tbLogs.Text += "OK.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Query/Generate error: " + ex);
            }
            IsEnabled = true;
        }

        private async Task<string> QueryEmbeddings(string query)
        {
            tbLogs.Text = "Generating embeddings using GPT...";
            EmbeddingsResponse embeddingsResponse = await openAiClient.EmbeddingsEndpoint.CreateEmbeddingAsync(query, EMBEDDING_MODEL);
            float[] queryEmbeddings = embeddingsResponse.Data.First().Embedding.Select(e => (float)e).ToArray();

            tbLogs.Text += "\nQuerying pinecone...";
            QueryRequest queryRequest = new QueryRequest
            {
                Vector = queryEmbeddings,
                TopK = 3,
                Namespace = NAMESPACE,
                IncludeMetadata = true
            };
            QueryResponse queryResponse = await indexClient.QueryAsync(queryRequest);
            if (queryResponse.Matches == null || !queryResponse.Matches.Any())
            {
                MessageBox.Show("No matches found!");
                return null;
            }

            tbLogs.Text += "\nGet matches: " + queryResponse.Matches.Count();
            tbOutput.Text = "";
            foreach (var match in queryResponse.Matches)
            {
                // print the text and score
                string text = match.Metadata?["text"].ToString() ?? "";
                text = System.Text.RegularExpressions.Regex.Unescape(text);
                string page = match.Metadata?["pageNumber"].ToString() ?? "";
                string filename = match.Metadata?["fileName"].ToString() ?? "";
                filename = System.Text.RegularExpressions.Regex.Unescape(filename);
                string msg = $"##score:{match.Score}\n##filename:{filename}\n##page:{page}\n##text:{text}\n";
                tbOutput.Text += msg;
            }

            return tbOutput.Text;
        }

        private async Task DeleteSelectedIds(string idPrefix)
        {
            var listResponse = await indexClient.ListAsync(new ListRequest
            {
                Prefix = idPrefix,
                Namespace= NAMESPACE,
                Limit = 100,
            });
            if (listResponse.Vectors == null || listResponse.Vectors.Any())
            {
                return;
            }

            var deleteResponse = await indexClient.DeleteAsync(new DeleteRequest
            {
                Ids = listResponse.Vectors.Select(x => x.Id).ToList(),
                Namespace = NAMESPACE,
            });
        }

        private async void BtnDelete_OnClick(object sender, RoutedEventArgs e)
        {
            List<string> selectedIds = InputFiles.Where(x => x.IsSelected).Select(x => x.PineconeId).ToList();

            if (selectedIds.Count > 0)
            {
                var ans = MessageBox.Show("Delete selected items?", "", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ans != MessageBoxResult.Yes)
                    return;
                foreach (string id in selectedIds)
                {
                    IsEnabled = false;
                    try
                    {
                        await DeleteSelectedIds(id);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Delete error: " + ex);
                    }
                    foreach (InputFileModel file in InputFiles.ToList())
                    {
                        if (file.IsSelected)
                            InputFiles.Remove(file);
                    }
                    IsEnabled = true;
                }
            }
        }
    }
}
