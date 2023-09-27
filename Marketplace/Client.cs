using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace XboxLib.Marketplace;

public class Client
{
    public const string LiveNamespace = "http://www.live.com/marketplace";
    public const string AtomNamespace = "http://www.w3.org/2005/Atom";
    public CultureInfo Language { get; set; }
    public CultureInfo Region { get; set; }
    private HttpClient _client;

    public Client()
    {
        Language = CultureInfo.CurrentCulture;
        Region = CultureInfo.CurrentCulture;
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("User-Agent", "Xbox Live Client/2.0.15574.0");
    }

    public async Task FindGames()
    {
        var pageSize = 100;
        var totalItem = 100;
        for (var page = 0; page * pageSize < totalItem; page++)
        {
            var values = new Dictionary<string, string[]>
            {
                { "Locale", new[] { Language.Name } },
                { "LegalLocale", new[] { Region.Name } },
                { "Store", new[] { "1" } },
                { "PageSize", new[] { pageSize.ToString() } },
                { "PageNum", new[] { (page + 1).ToString() } },
                { "DetailView", new[] { "5" } },
                { "OfferFilterLevel", new[] { "1" } },
                { "CategoryIDs", new[] { "3000" } },
                { "OrderBy", new[] { "1" } },
                { "OrderDirection", new[] { "1" } },
                { "ImageFormats", new[] { "5" } },
                { "ImageSizes", new[] { "15" } },
                { "UserTypes", new[] { "1", "2", "3" } },
                { "MediaTypes", new[] { "1", "21", "23", "37", "46" } }
            };

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("MethodName", "FindGames");
            foreach (var pair in values)
            {
                foreach (var value in pair.Value)
                {
                    query.Add("Names", pair.Key);
                    query.Add("Values", value);
                }
            }

            var uri = new UriBuilder("http", "catalog.xboxlive.com", 80, "/Catalog/Catalog.asmx/Query")
                { Query = query.ToString() }.ToString();

            var results = await _client.GetAsync(uri);
            var stream = await results.Content.ReadAsStreamAsync();
            using var file = File.Open("./example.xml", FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(file);
            await file.FlushAsync();
            break;
        }
    }
}