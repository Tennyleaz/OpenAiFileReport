using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace OpenAiFileReport
{
    /// <summary>
    /// Parses a PDF file and extracts the text from it.
    /// </summary>
    public class PDFParser
    {
        /// BT = Beginning of a text object operator 
        /// ET = End of a text object operator
        /// Td move to the start of next line
        ///  5 Ts = superscript
        /// -5 Ts = subscript

        #region Fields

        #region _numberOfCharsToKeep
        /// <summary>
        /// The number of characters to keep, when extracting text.
        /// </summary>
        private const int _numberOfCharsToKeep = 15;
        #endregion

        #endregion

        /// <summary>
        /// Extracts a text from a PDF file.
        /// </summary>
        /// <param name="inFileName">the full path to the pdf file.</param>
        /// <returns>the extracted text</returns>
        public List<string> ExtractText(string inFileName)
        {
            List<string> datas = new List<string>();
            try
            {
                using (PdfDocument document = PdfDocument.Open(inFileName))
                {
                    foreach (Page page in document.GetPages())
                    {
                        string data = string.Empty;
                        List<Line> lines = new List<Line>();
                        var currLine = new Line();
                        lines.Add(currLine);
                        foreach (Word word in page.GetWords())
                        {
                            var box = word.BoundingBox;
                            if (!currLine.InSameLine(word))
                            {
                                currLine = new Line();
                                lines.Add(currLine);
                            }
                            currLine.AddWord(word);
                        }
                        var leftMargin = lines.Min(l => l.Left);
                        foreach (var line in lines)
                        {
                            var indent = line.Left - leftMargin;
                            if (indent > 0)
                            {
                                //Console.Write(new string(' ', (int)indent / 14));
                                int count = (int)indent / 14;
                                for (int i = 0; i < count; i++)
                                    data += ' ';
                            }

                            data += "\n" + line;
                            //Console.WriteLine(line);
                        }
                        datas.Add(data);
                    }
                }

                return datas;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return datas;
            }
        }

    }

    public class Line
    {
        public List<Word> Words { get; set; } = new List<Word>();
        public double? Bottom { get; set; } = null;
        public double Left => Words.Min(w => w.BoundingBox.Left);
        public void AddWord(Word word)
        {
            if (!InSameLine(word))
                throw new Exception("Word is not in the same line");
            Words.Add(word);
            if (Bottom == null)
            {
                Bottom = word.BoundingBox.Bottom;
            }
            else
            {
                Bottom = Math.Max(Bottom.Value, word.BoundingBox.Bottom);
            }
        }
        public bool InSameLine(Word word)
        {
            return Bottom == null ||
                   Math.Abs(word.BoundingBox.Bottom - Bottom.Value) < word.BoundingBox.Height;
        }

        public string ToString(int leftMargin)
        {
            var sb = new StringBuilder();
            Word prevWord = null;
            var avgCharWidth = Convert.ToInt32(Words.Average(w => w.BoundingBox.Width / w.Text.Length));
            if (leftMargin > 0) sb.Append(new String(' ', (int)(Words[0].BoundingBox.Left - leftMargin) / avgCharWidth));
            foreach (var word in Words.OrderBy(w => w.BoundingBox.Left))
            {
                if (prevWord != null && word.BoundingBox.Left - prevWord.BoundingBox.Right > avgCharWidth)
                    sb.Append(new String(' ', (int)(word.BoundingBox.Left - prevWord.BoundingBox.Right) / avgCharWidth));
                sb.Append(word.Text + " ");
                prevWord = word;
            }
            return sb.ToString();
        }
        public override string ToString() => ToString(0);

    }
}
