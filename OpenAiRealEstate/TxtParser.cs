using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAiFileReport
{
    internal class TxtParser
    {
        public List<string> ExtractText(string inFileName)
        {
            List<string> datas = new List<string>();
            try
            {
                string[] lines = File.ReadAllLines(inFileName);
                // check for empty lines to split into paragraphs
                int start = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        if (i > start) // Avoid empty paragraphs from consecutive blank lines
                        {
                            string paragraph = string.Join("\n", lines.Skip(start).Take(i - start));
                            datas.Add(paragraph);
                        }
                        start = i + 1;
                    }
                }
                // Handle last paragraph (if the file doesn't end with an empty line)
                if (start < lines.Length)
                {
                    string lastParagraph = string.Join("\n", lines.Skip(start).Take(lines.Length - start));
                    datas.Add(lastParagraph);
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
