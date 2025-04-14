using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace OpenAiFileReport
{
    internal class TxtParser
    {
        public List<string> ExtractText(FileInfo fileInfo)
        {
            List<string> datas = new List<string>();
            try
            {
                string fileName = fileInfo.Name;
                string fileDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd");
                string[] lines = File.ReadAllLines(fileInfo.FullName);
                // check for empty lines to split into paragraphs
                int start = 0;
                int paragraphCount = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        if (i > start) // Avoid empty paragraphs from consecutive blank lines
                        {
                            lines[i] = RemovCJKWhitespace(lines[i]);
                            string paragraph = string.Join("\n", lines.Skip(start).Take(i - start));
                            paragraph = paragraph.Trim();
                            if (!string.IsNullOrWhiteSpace(paragraph))
                            {
                                // add filename and page info to data
                                List<string> results = SplitParagraph(paragraph);
                                for (int j = 0; j < results.Count; j++)
                                {
                                    paragraphCount++;
                                    string newParagraph = $"#File:{fileName} #Date:{fileDate} #Paragraph:{paragraphCount}\n{results[j]}";
                                    datas.Add(newParagraph);
                                }
                            }
                        }
                        start = i + 1;
                    }
                }
                // Handle last paragraph (if the file doesn't end with an empty line)
                if (start < lines.Length)
                {
                    string lastParagraph = string.Join("\n", lines.Skip(start).Take(lines.Length - start));
                    lastParagraph = RemovCJKWhitespace(lastParagraph);
                    lastParagraph = lastParagraph.Trim();
                    if (!string.IsNullOrWhiteSpace(lastParagraph))
                    {
                        List<string> results = SplitParagraph(lastParagraph);
                        for (int j = 0; j < results.Count; j++)
                        {
                            paragraphCount++;
                            string newParagraph = $"#File:{fileName} #Date:{fileDate} #Paragraph:{paragraphCount}\n{results[j]}";
                            datas.Add(newParagraph);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return datas;
        }

        /// <summary>
        /// split the input by \n if longer than 8192 tokens
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static List<string> SplitParagraph(string input)
        {
            List<string> result = new List<string>();

            // split the input by \n if longer than 8192 tokens
            const int MAX_TOKEN = 3000;
            if (input.Length >= MAX_TOKEN)
            {
                string[] paragraphs = input.Split(new[] { "\n", "。", ". " }, StringSplitOptions.RemoveEmptyEntries);
                string temp = string.Empty;
                foreach (string paragraph in paragraphs)
                {
                    if (paragraph.Length > MAX_TOKEN)
                    {
                        // Split the paragraph into smaller chunks
                        for (int i = 0; i < paragraph.Length; i += MAX_TOKEN)
                        {
                            string chunk = paragraph.Substring(i, Math.Min(MAX_TOKEN, paragraph.Length - i));
                            result.Add(chunk);
                        }
                    }
                    else
                    {
                        // add each paragraph up to max length
                        if (temp.Length + paragraph.Length > MAX_TOKEN)
                        {
                            result.Add(temp);
                            temp = string.Empty;
                        }
                        else
                        {
                            temp += paragraph + ". ";
                        }
                    }
                }
            }
            else
            {
                result.Add(input);
            }

            return result;
        }

        private static string RemovCJKWhitespace(string input)
        {
            if (input == null)
                return string.Empty;
            for (int i = input.Length - 1; i >= 2; i--)
            {
                // remove whitespace before CJK characters
                if (isCJK(input[i]) && input[i-1] == ' ' && isCJK(input[i-2]))
                {
                    input = input.Remove(i - 1, 1);
                }
            }
            return input;
        }

        private static bool isCJK(char c)
        {
            // between U+2E80 ~ U+9FFF
            if (c >= 0x2E80 && c <= 0x9FFF)
            {
                return true;
            }
            return false;
        }
    }
}
