using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAiFileReport
{
    internal class DocumentInfo
    {
        public string FileName { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return $"###FileName: {FileName}, ###Text: {Text}";
        }
    }
}
