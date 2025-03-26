using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAiFileReport
{
    internal class ResponseSchema
    {
        public bool success { get; set; }
        public string filled_template { get; set; }
        public string error { get; set; }
    }
}
