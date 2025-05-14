using OpenQA.Selenium;
using OpenQA.Selenium.BiDi.Communication;
using OpenQA.Selenium.Edge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAiFileReport
{
    internal class Scrapper
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
        }
    }
}
