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
                int i = 0;
                for (i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        if (i > start) // Avoid empty paragraphs from consecutive blank lines
                        {
                            string paragraph = string.Join("\n", lines.Skip(start).Take(i - start));
                            paragraph = paragraph.Trim();
                            if (!string.IsNullOrWhiteSpace(paragraph))
                            {
                                // add filename and page info to data
                                paragraph = $"#File:{fileName} #Date:{fileDate} #Paragraph:{i}\n{paragraph}";
                                datas.Add(paragraph);
                            }
                        }
                        start = i + 1;
                    }
                }
                // Handle last paragraph (if the file doesn't end with an empty line)
                if (start < lines.Length)
                {
                    string lastParagraph = string.Join("\n", lines.Skip(start).Take(lines.Length - start));
                    lastParagraph = lastParagraph.Trim();
                    if (!string.IsNullOrWhiteSpace(lastParagraph))
                    {
                        lastParagraph = $"#File:{fileName} #Date:{fileDate} #Paragraph:{i}\n{lastParagraph}";
                        datas.Add(lastParagraph);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return datas;
        }
    }
}
