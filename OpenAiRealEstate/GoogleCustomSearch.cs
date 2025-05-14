using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace OpenAiFileReport
{
    internal class GoogleCustomSearch
    {
        private const string SEARCH_ENGINE_ID = "276592604f580469e";
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GoogleCustomSearch(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("Google search API key cannot be null or empty.", nameof(apiKey));
            }
            _apiKey = apiKey;
            _httpClient = new HttpClient();
        }

        public async Task<List<GoogleSearchResult>> SearchAsync(string query)
        {
            string url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={SEARCH_ENGINE_ID}&q={Uri.EscapeDataString(query)}";
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var searchResults = JsonSerializer.Deserialize<GoogleSearch>(json);
                return searchResults.items;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SearchAsync() error: " + ex);
                return null;
            }
        }

        public List<GoogleSearchResult> Search(string query, bool isLatest)
        {
            try
            {
                string url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={SEARCH_ENGINE_ID}&q={Uri.EscapeDataString(query)}";
                if (isLatest)
                    url += "&sort=date-sdate:d:s";
                WebRequest request = WebRequest.Create(url);
                request.Headers.Add(HttpRequestHeader.AcceptLanguage, "application/json;charset=UTF-8");
                request.Method = "GET";

                using (WebResponse response = request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    var searchResults = JsonSerializer.Deserialize<GoogleSearch>(json);
                    return searchResults.items;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Search() error: " + ex);
                return null;
            }
        }
    }

    internal class GoogleSearch
    {
        public string kind { get; set; }
        public GoogleSearchUrl url { get; set; }
        public GoogleSearchQuery queries { get; set; }
        public GoogleSearchContext context { get; set; }
        public List<GoogleSearchResult> items { get; set; }
    }

    internal class GoogleSearchContext
    {
        public string title { get; set; }
    }

    internal class GoogleSearchResult
    {
        public string kind { get; set; }
        public string title { get; set; }
        public string htmlTitle { get; set; }
        public string link { get; set; }
        public string displayLink { get; set; }
        public string snippet { get; set; }
        public string htmlSnippet { get; set; }
        public string cacheId { get; set; }
        public string formattedUrl { get; set; }
        public string htmlFormattedUrl { get; set; }
        //public string pagemap { get; set; }
        public string mime { get; set; }
        public string fileFormat { get; set; }
        public GoogleSearchImage image { get; set; }
        public List<GoogleSearchLabel> labels { get; set; }
    }
    
    internal class GoogleSearchUrl
    {
        public string type { get; set; }
        public string template { get; set; }
    }

    internal class GoogleSearchQuery
    {
        public List<GoogleSearchPage> previousPage { get; set; }
        public List<GoogleSearchPage> request { get; set; }
        public List<GoogleSearchPage> nextPage { get; set; }
    }

    internal class GoogleSearchPage
    {
        public string title { get; set; }
        public string totalResults { get; set; }
        public string searchTerms { get; set; }
        public int count { get; set; }
        public int startIndex { get; set; }
        public string startPage { get; set; }
        public string language { get; set; }
        public string inputEncoding { get; set; }
        public string outputEncoding { get; set; }
        public string safe { get; set; }
        public string cx { get; set; }
        public string sort { get; set; }
        public string filter { get; set; }
        public string gl { get; set; }
        public string cr { get; set; }
        public string googleHost { get; set; }
        public string disableCnTwTranslation { get; set; }
        public string hq { get; set; }
        public string hl { get; set; }
        public string siteSearch { get; set; }
        public string siteSearchFilter { get; set; }
        public string exactTerms { get; set; }
        public string excludeTerms { get; set; }
        public string linkSite { get; set; }
        public string orTerms { get; set; }
        public string relatedSite { get; set; }
        public string dateRestrict { get; set; }
        public string lowRange { get; set; }
        public string highRange { get; set; }
        public string fileType { get; set; }
        public string rights { get; set; }
        public string searchType { get; set; }
        public string imgSize { get; set; }
        public string imgType { get; set; }
        public string imgColorType { get; set; }
        public string imgDominantColor { get; set; }
    }

    internal class GoogleSearchImage
    {
        public string contextLink { get; set; }
        public string height { get; set; }
        public string width { get; set; }
        public string byteSize { get; set; }
        public string thumbnailLink { get; set; }
        public string thumbnailHeight { get; set; }
        public string thumbnailWidth { get; set; }
    }

    internal class GoogleSearchLabel
    {
        public string name { get; set; }
        public string displayName { get; set; }
        public string label_with_op { get; set; }
    }
}
