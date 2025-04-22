using AssemblyAI;
using AssemblyAI.Transcripts;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Threading;
using MdXaml;

namespace OpenAiFileReport;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string _openAiKey = "", _assemblyAiKey = "";
    internal ObservableCollection<InputFileModel> InputFiles = new ObservableCollection<InputFileModel>();
    private FileInfo audioFileInfo, templateFileInfo;
    private AssemblyAIClient assemblyAiClient;
    private OpenAIClient openAiClient;
    private CancellationTokenSource cts;
    private readonly DispatcherTimer textChangeTimer;

    public MainWindow()
    {
        InitializeComponent();

        textChangeTimer = new DispatcherTimer();
        textChangeTimer.Tick += TextChangeTimer_Tick;
        textChangeTimer.Interval = TimeSpan.FromMilliseconds(600);
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _openAiKey = File.ReadAllText("openai_key.txt").Trim();
            _assemblyAiKey = File.ReadAllText("assembly_key.txt").Trim();
            assemblyAiClient = new AssemblyAIClient(_assemblyAiKey);

            // custom http client with longer timeout
            HttpClient httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            openAiClient = new OpenAIClient(new OpenAIAuthentication(_openAiKey), null, httpClient);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Faild to read keys from txt!\n" + ex.Message);
        }

        //inputFileListBox.ItemsSource = InputFiles;
        //InputFiles.Add(new InputFileModel()
        //{
        //    FileName = "Test.txt",
        //    IsSelected = false
        //});
        cbLocale.ItemsSource = localesBest;
        cbLocale.SelectedIndex = 0;

        //IReadOnlyList<OpenAI.Models.Model> models = await openAiClient.ModelsEndpoint.GetModelsAsync();
        //cbModelNames.ItemsSource = models.Select(x => x.Id);
    }

    private void BtnAddFile_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Filter = "Text Files (*.txt)|*.txt|Wave Files (*.wav)|*.wav";
        dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        dialog.Title = "Select your file:";
        if (dialog.ShowDialog() != true)
            return;

        FileInfo fileInfo = new FileInfo(dialog.FileName);
        if (fileInfo.Exists)
        {
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
        }
    }

    private void BtnAudioFile_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Filter = "WAV files (*.wav)|*.wav| MP3 files (*.mp3)|*.mp3| AAC files (*.aac)|*.aac";
        dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        dialog.Title = "Select an audio file";
        if (dialog.ShowDialog() != true)
            return;

        audioGrid.IsEnabled = true;
        audioFileInfo = new FileInfo(dialog.FileName);
        lbAudioName.Content = audioFileInfo.Name;
        lbPasteHint.Visibility = Visibility.Collapsed;
    }

    private async void BtnStt_OnClick(object sender, RoutedEventArgs e)
    {
        if (audioFileInfo == null)
            return;

        // Transcribe file at remote URL
        TranscriptLanguageCode transcriptLanguageCode = (TranscriptLanguageCode)Enum.Parse(typeof(TranscriptLanguageCode), Locale, true);
        TranscriptOptionalParams tp = new TranscriptOptionalParams
        {
            //AudioUrl = uri.ToString(),
            LanguageCode = transcriptLanguageCode,
            //LanguageDetection = chkAutoDetect.IsChecked,
            SpeechModel = SpeechModel.Best,
            SpeakerLabels = true,
            FormatText = true,
            //Punctuate = true,
            //AutoChapters = chkAutoChapter.IsChecked,
            //Summarization = chkSummary.IsChecked
        };
        if (cbSpeakerCount.SelectedIndex > 0)
        {
            tp.SpeakersExpected = cbSpeakerCount.SelectedIndex + 1;
        }

        audioGrid.IsEnabled = false;
        btnAudioFile.IsEnabled = false;
        progress.Visibility = Visibility.Visible;
        cts = new CancellationTokenSource();
        try
        {
            RequestOptions options = new RequestOptions
            {
                Timeout = TimeSpan.FromMinutes(10),
            };
            var transcript = await assemblyAiClient.Transcripts.TranscribeAsync(audioFileInfo, tp, options, cts.Token);

            // checks if transcript.Status == TranscriptStatus.Completed, throws an exception if not
            transcript.EnsureStatusCompleted();
            await ShowResult(transcript);
            // delete it on server
            //transcript = await client.Transcripts.DeleteAsync(transcript.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Transcribe error: " + ex.Message);
            audioGrid.IsEnabled = true;
            btnAudioFile.IsEnabled = true;
        }
        progress.Visibility = Visibility.Collapsed;
    }

    private async Task ShowResult(Transcript transcript)
    {
        if (transcript.Utterances != null && transcript.Utterances.Any())
        {
            foreach (TranscriptUtterance tu in transcript.Utterances)
            {
                TimeSpan start = TimeSpan.FromMilliseconds(tu.Start);
                TimeSpan end = TimeSpan.FromMilliseconds(tu.End);
                //string timeText = $"[{start.ToString(@"mm\:ss")}-{end.ToString(@"mm\:ss")}]";
                tbConversationText.Text += tu.Speaker + ": " + tu.Text + "\n";
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(transcript.Summary))
            {
                tbConversationText.Text += $"[Summary]\n{transcript.Summary}\n\n";
            }
            if (transcript.Chapters != null && transcript.Chapters.Any())
            {
                int index = 0;
                foreach (Chapter chapter in transcript.Chapters)
                {
                    index++;
                    tbConversationText.Text += $"[Chapter {index}]\n{chapter.Headline}\n";
                }
            }
            tbConversationText.Text += transcript.Text;
        }

        // use chat completion to add missing Chinese punctuation marks
        if (Locale == "Zh" || Locale == "Ja")
        {
            string systemPrompt = "Format the given Chinese/Japanese text. Add missing Chinese/Japanese punctuation marks, and remove redundant whitespaces. Do not change the content if possible.";
            string userMessage = tbConversationText.Text.Trim();
            ChatRequest chatRequest = new ChatRequest(
                model: "gpt-4o-mini",
                messages: new List<Message>()
                {
                    new Message(Role.System, systemPrompt),
                    new Message(Role.User, userMessage)
                }
            );
            ChatResponse chatResponse = await openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest, cts.Token);
            string message = chatResponse.FirstChoice.Message;
            tbConversationText.Text = message;
        }

        CheckReportReady();
    }

    private readonly List<string> localesBest = new List<string>()
    {
        "En",
        "EnAu",
        "EnUk",
        "EnUs",
        "Es",
        "Fr",
        "De",
        "It",
        "Pt",
        "Nl",
        "Hi",
        "Ja",
        "Zh",
        "Fi",
        "Ko",
        "Pl",
        "Ru",
        "Tr",
        "Uk",
        "Vi"
    };

    public string Locale
    {
        get
        {
            return localesBest[cbLocale.SelectedIndex];
        }
    }

    private void BtnTemplateFile_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt";
        dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        dialog.Title = "Select a template file";
        if (dialog.ShowDialog() != true)
            return;

        templateGrid.IsEnabled = true;
        templateFileInfo = new FileInfo(dialog.FileName);
        lbTemplateName.Content = templateFileInfo.Name;

        CheckReportReady();
    }

    private void CheckReportReady()
    {
        if (!string.IsNullOrEmpty(tbConversationText.Text) && templateFileInfo != null)
        {
            btnGenerateReport.IsEnabled = true;
        }
    }

    private async void BtnGenerateReport_OnClick(object sender, RoutedEventArgs e)
    {
        string templateContnet = (await File.ReadAllTextAsync(templateFileInfo.FullName)).Trim();
        string conversationContent = tbConversationText.Text.Trim();
        string userMessage = $"### Conversation Record:\n{conversationContent}\n\n### User's Report Template:\n{templateContnet}";

        string js = @"{""type"":""object"",""properties"":{""success"":{""type"":""boolean""},""filled_template"":{""type"":""string""},""error"":{""type"":""string"",""nullable"":true}},""required"":[""success"",""filled_template"",""error""],""additionalProperties"":false}";
        JsonNode schema = JsonNode.Parse(js);

        string systemPrompt = "Generate a structured report using the provided template. Fill in the placeholders with relevant content from the conversation record.";
        GeneratePromptWin gpw = new GeneratePromptWin(systemPrompt);
        gpw.Owner = this;
        gpw.ShowDialog();
        systemPrompt = gpw.Prompt;

        ChatRequest chatRequest = new ChatRequest(
            model: "gpt-4o-mini",
            messages: new List<Message>()
            {
                new Message(Role.System, systemPrompt),
                new Message(Role.User, userMessage)
            },
            responseFormat: ChatResponseFormat.JsonSchema,
            jsonSchema: new JsonSchema("report_schema", schema)
        );

        cts = new CancellationTokenSource();
        progress.Visibility = Visibility.Visible;
        sourcePanel.IsEnabled = false;
        tbConversationText.IsEnabled = false;
        try
        {
            ChatResponse chatResponse = await openAiClient.ChatEndpoint.GetCompletionAsync(chatRequest, cts.Token);
            string message = chatResponse.FirstChoice.Message;
            ResponseSchema structuredResponse = JsonSerializer.Deserialize<ResponseSchema>(message);
            if (structuredResponse.success)
            {
                //tbConversationText.Text = structuredResponse.filled_template;
                ShowMd(structuredResponse.filled_template);
            }
            else
            {
                MessageBox.Show("GPT error: " + structuredResponse.error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Generate report error: " + ex.Message);
        }
        progress.Visibility = Visibility.Collapsed;
        sourcePanel.IsEnabled = true;
        tbConversationText.IsEnabled = true;
    }


    private void TbConversationText_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        lbPasteHint.Visibility = Visibility.Collapsed;
        textChangeTimer.Stop();
        textChangeTimer.Start();
    }

    private void TextChangeTimer_Tick(object sender, EventArgs e)
    {
        CheckReportReady();
    }

    private void ShowMd(string markdownTxt)
    {
        mdGrid.Visibility = Visibility.Visible;
        mdxamViewer.Markdown = markdownTxt;
    }

    private void BtnCloseMd_OnClick(object sender, RoutedEventArgs e)
    {
        mdGrid.Visibility = Visibility.Collapsed;
    }

    private void BtnSaveMd_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Markdown files (*.md)|*.md";
            dialog.OverwritePrompt = true;
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, mdxamViewer.Markdown);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Save failed: " + ex.Message);
        }
    }
}