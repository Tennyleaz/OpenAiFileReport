using System;
using System.Collections;
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
        //private readonly Guid userId;
        private const string indexName = "rag-demo";
        private readonly OpenAIClient openAiClient;
        private readonly PineconeClient pineconeClient;
        private readonly string tempFolder;  // %localappdata%/OpenAiTempFiles
        private IndexClient indexClient;
        private Pinecone.Index myIndex;
        internal ObservableCollection<InputFileModel> InputFiles = new ObservableCollection<InputFileModel>();
        private const string EMBEDDING_MODEL = "text-embedding-ada-002";
        private const int DIMENTION = 1536;
        private readonly string NAMESPACE;
        private string retrievedText;

        public FileSearchDemo()
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;
            IsEnabled = false;
            if (string.IsNullOrEmpty(Properties.Settings.Default.UserId) || Properties.Settings.Default.UserId == Guid.Empty.ToString())
            {
                Properties.Settings.Default.UserId = Guid.NewGuid().ToString();
                Properties.Settings.Default.Save();
            }
            NAMESPACE = "user-" + Properties.Settings.Default.UserId.Substring(0,8);
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
            lbYourName.Content = "You: " + NAMESPACE;
            LoadFileList();
            inputFileListBox.ItemsSource = InputFiles;
            
            // load settings
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.SystemPromptVector))
            {
                tbSystemPromptVector.Text = Properties.Settings.Default.SystemPromptVector;
            }
            else
            {
                string systemPrompt = await File.ReadAllTextAsync("Reformat System Prompt.txt");
                tbSystemPromptVector.Text = systemPrompt.Trim();
            }
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.SystemPromptSummarize))
            {
                tbSystemPromptSum.Text = Properties.Settings.Default.SystemPromptSummarize;
            }
            else
            {
                tbSystemPromptSum.Text = "You are an expert assistant that helps users summarize and interpret retrieved text from document searches. " +
                                         "Site which file/segment if possible.";
            }
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.SystemPromptFormat))
            {
                tbSystemPromptFormat.Text = Properties.Settings.Default.SystemPromptFormat;
            }
            else
            {
                tbSystemPromptFormat.Text = "You are an expert assistant that reformat short user prompt into proper user prompt with instructions. " +
                                            "This user prompt will combine user's vector database search result with document chunks, then feed to GPT to generate a summary.";
            }
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.UserPrompt))
            {
                tbUserPrompt.Text = Properties.Settings.Default.UserPrompt;
            }

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
            
            DescribeIndexStatsResponse r = await indexClient.DescribeIndexStatsAsync(new DescribeIndexStatsRequest());
            if (r.Namespaces != null)
            {
                if (r.Namespaces.TryGetValue(NAMESPACE, out NamespaceSummary ns))
                {
                    tbLogs.Text += "\nVectors: " + ns.VectorCount;
                }
                else
                {
                    tbLogs.Text += "\nVectors: 0";
                }
            }
            IsEnabled = true;
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

                // upload to pinecone for bigger files
                IsEnabled = false;
                string documentId = await ReadDocument(fileInfo);
                if (documentId == null)
                {
                    MessageBox.Show("Failed to add document!");
                }
                else
                {
                    // add to list
                    InputFileModel newFile = new InputFileModel()
                    {
                        FullPath = fileInfo.FullName,
                        FileType = fileInfo.Extension,
                        FileName = fileInfo.Name,
                    };
                    newFile.PineconeId = documentId;
                    newFile.IsProcessed = true;
                    InputFiles.Add(newFile);
                }
                IsEnabled = true;
            }
        }

        private async Task<string> ReadDocument(FileInfo fileInfo)
        {
            List<string> data;
            string documentText;
            if (fileInfo.Extension == ".pdf")
            {
                tbLogs.Text = "Reading PDF file...";
                PDFParser parser = new PDFParser();
                data = parser.ExtractText(fileInfo);
                documentText = parser.ExtractWhole(fileInfo);
            }
            else if (fileInfo.Extension == ".txt")
            {
                tbLogs.Text = "Reading TXT file...";
                TxtParser parser = new TxtParser();
                data = parser.ExtractText(fileInfo);
                documentText = parser.ExtractWhole(fileInfo);
            }
            else
            {
                MessageBox.Show("Does not supoort this file: " + fileInfo.Extension);
                return null;
            }
            string documentId = "File-" + Guid.NewGuid().ToString();
            tbLogs.Text = "Generating embeddings using GPT...";
            List<float[]> embeddings;
            try
            {
                embeddings = await GetImbeddings(data);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Create Embedding error:" + ex.Message);
                tbLogs.Text = ex.ToString();
                return null;
            }

            // ensure count is same
            if (embeddings == null || embeddings.Count != data.Count)
            {
                MessageBox.Show("Error in embeddings count!");
                return null;
            }

            tbLogs.Text = "Generating summary using GPT...";
            try
            {
                await SaveSummary(documentText, fileInfo.Name, documentId);
            }
            catch (Exception ex)
            {
                MessageBox.Show("SaveSummary error!\n" + ex);
                tbLogs.Text = ex.ToString();
                return null;
            }

            tbLogs.Text = "Upload embeddings to pinecone...";
            try
            {
                await StoreEmbeddings(documentId, data, embeddings, fileInfo.Name);
                tbLogs.Text = "OK.";
                return documentId;
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
                        ["text"] = data[i],
                        ["type"] = "chunk"
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

        private async Task<string> GenerateSummary(string documentText)
        {
            ChatRequest chatRequest = new ChatRequest(
                model: "gpt-4o-mini",
                messages: new List<Message>()
                {
                    new Message(Role.System, "Summarize a document for the user. Use user's input language if possible."),
                    new Message(Role.User, documentText)
                }
            );
            ChatResponse chatResponse = await openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            return chatResponse.Choices.First().ToString();
        }

        private async Task SaveSummary(string documentText, string fileName, string vectorid)
        {
            string summary = await GenerateSummary(documentText);
            EmbeddingsResponse embeddingResponse = await openAiClient.EmbeddingsEndpoint.CreateEmbeddingAsync(summary, EMBEDDING_MODEL);
            // get the embeddings double array
            List<float[]> embeddings = new List<float[]>();
            foreach (Datum dt in embeddingResponse.Data)
            {
                float[] embedding = dt.Embedding.Select(x => (float)x).ToArray();
                embeddings.Add(embedding);
            }
            // save to pinecone
            List<Vector> vectors = new List<Vector>();
            // we should have only 1 item in vectors
            for (int i = 0; i < embeddings.Count; i++)
            {
                vectors.Add(new Vector
                {
                    Id = vectorid + "-summary",
                    Values = embeddings[i],
                    Metadata = new Metadata
                    {
                        ["fileName"] = fileName,
                        ["text"] = summary,
                        ["type"] = "summary"
                    },
                });
            }

            UpsertRequest request = new UpsertRequest()
            {
                Vectors = vectors,
                Namespace = NAMESPACE,
            };
            UpsertResponse upsertResponse = await indexClient.UpsertAsync(request);
            Console.WriteLine(upsertResponse.UpsertedCount);
        }

        private void FileSearchDemo_OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveFileList();
            if (!string.IsNullOrWhiteSpace(tbSystemPromptVector.Text))
                Properties.Settings.Default.SystemPromptVector = tbSystemPromptVector.Text;
            if (!string.IsNullOrWhiteSpace(tbUserPrompt.Text))
                Properties.Settings.Default.UserPrompt = tbUserPrompt.Text;
            if (!string.IsNullOrWhiteSpace(tbSystemPromptSum.Text))
                Properties.Settings.Default.SystemPromptSummarize = tbSystemPromptSum.Text;
            Properties.Settings.Default.Save();
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

        private async void BtnAskSummarize_OnClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(retrievedText) && !string.IsNullOrWhiteSpace(tbSummarizeUserPrompt.Text) && !string.IsNullOrWhiteSpace(tbSystemPromptSum.Text))
            {
                IsEnabled = false;
                tbLogs.Text = "Asking GPT...";
                string userPrompt = $"User query: {tbSummarizeUserPrompt.Text}\n\nMatched text from vector store:\n{retrievedText}.";
                ChatRequest chatRequest = new ChatRequest(
                    model: Model,
                    messages: new List<Message>()
                    {
                        new Message(Role.System, tbSystemPromptSum.Text),
                        new Message(Role.User, userPrompt)
                    }
                    //tools: chatTools
                    //responseFormat: ChatResponseFormat.JsonSchema,
                    //jsonSchema: new JsonSchema("report_schema", schema)
                );
                ChatResponse chatResponse = await openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                tbOutput.Text = chatResponse.Choices.First().ToString();
                tbLogs.Text += "OK.";
                IsEnabled = true;
            }
            else
            {
                MessageBox.Show("retrievedText is null!");
            }
        }

        private async void BtnSearchPinecone_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbPineconeSearch.Text))
            {
                MessageBox.Show("Pinecone search query is empty!");
                return;
            }
            IsEnabled = false;
            try
            {
                retrievedText = null;
                retrievedText = await QueryEmbeddings(tbPineconeSearch.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Query error: " + ex);
            }
            IsEnabled = true;
        }

        private async void BtnFormatSummaryQuery_OnClick(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            try
            {
                tbSummarizeUserPrompt.Text = string.Empty;
                tbLogs.Text = "Reformat user prompt...";
                tbSummarizeUserPrompt.Text = await ReFormatQuery(tbUserPrompt.Text, tbSystemPromptVector.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Format error: " + ex);
            }
            IsEnabled = true;
        }

        private async void BtnFormatVector_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbSystemPromptVector.Text) || string.IsNullOrEmpty(tbUserPrompt.Text))
            {
                MessageBox.Show("Prompts are empty!");
                return;
            }
            IsEnabled = false;
            try
            {
                //List<Tool> chatTools = new List<Tool>();
                //Function function = Function.FromFunc<string, string>(
                //    "get_summary",
                //    GetSummary,
                //    "Get summary of a document by file name.",
                //    true);
                //chatTools.Add(function);
                tbPineconeSearch.Text = string.Empty;
                tbLogs.Text = "Reformat query...";
                string userPrompt = $"Metadata: {GenerateMetadata()}\nUser prompt: {tbUserPrompt.Text.Trim()}";
                tbPineconeSearch.Text = await ReFormatQuery(userPrompt, tbSystemPromptVector.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Query/Generate error: " + ex);
            }
            IsEnabled = true;
        }

        private Metadata CreateFileFilter()
        {
            // see:
            // https://docs.pinecone.io/guides/data/understanding-metadata
            // $in Matches vectors with metadata values that are in a specified array.
            // Example: {"genre": {"$in": ["comedy", "documentary"]}}
            List<string> selectedFileNames = InputFiles.Where(x => x.IsSelected).Select(x => x.FileName).ToList();
            if (selectedFileNames.Count > 0)
            {
                // filter by query input
                //if (queryFilter != null && queryFilter.Length > 0)
                //{
                //    for (int i = selectedFileNames.Count - 1; i >= 0; i--)
                //    {
                //        if (!queryFilter.Contains(selectedFileNames[i]))
                //            selectedFileNames.RemoveAt(i);
                //    }
                //}

                if (selectedFileNames.Count > 0)
                {
                    return new Metadata
                    {
                        ["fileName"] = new Metadata
                        {
                            ["$in"] = selectedFileNames
                        }
                    };
                }
            }
            return null;
        }

        private uint DetermineTopK()
        {
            int selected = InputFiles.Count(x => x.IsSelected);
            if (selected <= 2)
                return 5;
            if (selected >= 10)
                return 15;
            return (uint)(selected + 4);
        }

        private string GenerateMetadata()
        {
            IEnumerable<InputFileModel> selected = InputFiles.Where(x => x.IsSelected);
            if (!selected.Any())
                selected = InputFiles;
            string metadata = "";
            foreach (InputFileModel file in selected)
            {
                metadata += $" - FileName:{file.FileName}\tLastModified:{file.LastModified}\n";
            }
            return metadata;
        }

        private async Task<string> ReFormatQuery(string userPrompt, string systemPrompt)
        {
            
            //string schema = await File.ReadAllTextAsync("reformat_schema.json");
            ChatRequest chatRequest = new ChatRequest(
                model: "gpt-4o-mini",
                messages: new List<Message>()
                {
                    new Message(Role.System, systemPrompt),
                    new Message(Role.User, userPrompt)
                }
                //responseFormat: ChatResponseFormat.JsonSchema,
                //jsonSchema: new JsonSchema("reformat_schema", schema)
            );
            ChatResponse chatResponse = await openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            string jsonResult = chatResponse.Choices.First().ToString();
            //return JsonSerializer.Deserialize<ReformatSchema>(jsonResult);
            return jsonResult;
        }

        private async Task<string> QueryEmbeddings(string reformatted)
        {
            tbLogs.Text += "\nGenerating embeddings using GPT...";
            EmbeddingsResponse embeddingsResponse = await openAiClient.EmbeddingsEndpoint.CreateEmbeddingAsync(reformatted, EMBEDDING_MODEL);
            float[] queryEmbeddings = embeddingsResponse.Data.First().Embedding.Select(e => (float)e).ToArray();

            tbLogs.Text += "\nQuerying pinecone...";
            QueryRequest queryRequest = new QueryRequest
            {
                Vector = queryEmbeddings,
                TopK = DetermineTopK(),
                Namespace = NAMESPACE,
                IncludeMetadata = true,
                Filter = CreateFileFilter()
            };
            QueryResponse queryResponse = await indexClient.QueryAsync(queryRequest);
            if (queryResponse.Matches == null || !queryResponse.Matches.Any())
            {
                MessageBox.Show("No matches found!");
                return null;
            }

            tbLogs.Text += "\nGet matches: " + queryResponse.Matches.Count();
            tbOutput.Text = "";
            string queryResult = "";
            List<string> queryText = new List<string>();
            foreach (var match in queryResponse.Matches)
            {
                if (match.Metadata == null)
                    continue;

                // print the text and score
                string text = match.Metadata["text"].ToString();
                text = System.Text.RegularExpressions.Regex.Unescape(text);
                
                string filename = match.Metadata["fileName"].ToString();
                filename = System.Text.RegularExpressions.Regex.Unescape(filename);
                string msg = $"##score:{match.Score}\n##filename:{filename}\n";
                
                if (match.Metadata.TryGetValue("pageNumber", out var value))
                {
                    string page = value.ToString();
                    msg += $"##page:{page}\n";
                }

                msg += $"##text:{text}\n";
                //tbOutput.Text += msg;
                queryResult += msg;
                queryText.Add(msg);
            }

            // show temp result 
            QueryResultWindow queryResultWindow = new QueryResultWindow(queryText);
            queryResultWindow.Show();

            return queryResult;
        }

        private async Task DeleteSelectedIds(string idPrefix)
        {
            if (string.IsNullOrEmpty(idPrefix))
                return;
            var listResponse = await indexClient.ListAsync(new ListRequest
            {
                Prefix = idPrefix,
                Namespace= NAMESPACE,
                Limit = 100,
            });
            if (listResponse.Vectors == null || !listResponse.Vectors.Any())
            {
                return;
            }

            List<string> idList = listResponse.Vectors.Select(x => x.Id).ToList();
            var deleteResponse = await indexClient.DeleteAsync(new DeleteRequest
            {
                Ids = idList,
                Namespace = NAMESPACE,
            });
            tbLogs.Text = "Deleted " + idList.Count + " items.";
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

        private string Model
        {
            get
            {
                if (cbModel.SelectedIndex == 1)
                    return "gpt-4o-mini";
                return "gpt-4o";
            }
        }
    }
}
