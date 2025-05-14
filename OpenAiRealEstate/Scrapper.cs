using OpenQA.Selenium;
using OpenQA.Selenium.BiDi.Communication;
using OpenQA.Selenium.Edge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenAiFileReport
{
    internal class Scrapper : IDisposable
    {
        private readonly WebDriver webDriver;

        public Scrapper()
        {
            var options = new EdgeOptions();
            options.AddArguments(new List<string>() {
                "headless",
            });
            webDriver = new EdgeDriver(options);
        }

        public string Navigate(string url)
        {
            webDriver.Navigate().GoToUrl(url);
            Thread.Sleep(200);
            //string script = "";
            //object obj = webDriver.ExecuteAsyncScript(script);
            //if (obj != null)
            //{

            //}
            //else
            //{

            //}
            return webDriver.PageSource;
            return HTMLToText(webDriver.PageSource);
        }

        public static string HTMLToText(string HTMLCode)
        {
            // Remove new lines since they are not visible in HTML  
            HTMLCode = HTMLCode.Replace("\n", " ");
            // Remove tab spaces  
            HTMLCode = HTMLCode.Replace("\t", " ");
            // Remove multiple white spaces from HTML  
            HTMLCode = Regex.Replace(HTMLCode, "\\s+", " ");
            // Remove HEAD tag  
            HTMLCode = Regex.Replace(HTMLCode, "<head.*?</head>", ""
                , RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // Remove any JavaScript  
            HTMLCode = Regex.Replace(HTMLCode, "<script.*?</script>", ""
                , RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // Replace special characters like &, <, >, " etc.  
            StringBuilder sbHTML = new StringBuilder(HTMLCode);
            // Note: There are many more special characters, these are just  
            // most common. You can add new characters in this arrays if needed  
            string[] OldWords = {"&nbsp;", "&amp;", "&quot;", "&lt;",
                "&gt;", "&reg;", "&copy;", "&bull;", "&trade;","&#39;"};
            string[] NewWords = { " ", "&", "\"", "<", ">", "Â®", "Â©", "â€¢", "â„¢", "\'" };
            for (int i = 0; i < OldWords.Length; i++)
            {
                sbHTML.Replace(OldWords[i], NewWords[i]);
            }
            // Check if there are line breaks (<br>) or paragraph (<p>)  
            sbHTML.Replace("<br>", "\n<br>");
            sbHTML.Replace("<br ", "\n<br ");
            sbHTML.Replace("<p ", "\n<p ");
            // Finally, remove all HTML tags and return plain text  
            return System.Text.RegularExpressions.Regex.Replace(
                sbHTML.ToString(), "<[^>]*>", "");
        }

        public void Dispose()
        {
            webDriver?.Close();
            webDriver?.Dispose();
        }
    }
}
