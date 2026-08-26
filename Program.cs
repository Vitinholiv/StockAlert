using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization.Metadata;

public class ApiManager {
    private static readonly HttpClient _client = new();
    private readonly string _token;

    public List<decimal> PriceHistory { get; private set; }

    public ApiManager(string token){
        _token = token;
        PriceHistory = [];
    }

    public async Task<decimal> GetStockValue(string asset)
    {
        string url = $"https://brapi.dev/api/v2/stocks/quote?symbols={asset}&token={_token}";

        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement root = doc.RootElement;
        decimal currPrice = root
            .GetProperty("results")[0]
            .GetProperty("data")
            .GetProperty("regularMarketPrice")
            .GetDecimal();

        PriceHistory.Add(currPrice);
        return currPrice;
    }
}

class Program
{
    static async Task Main(string[] args){
        var config = JsonNode.Parse(File.ReadAllText("config.json"));
        string token = config?["ApiToken"]?.ToString() ?? "";

        var api = new ApiManager(token);

        decimal price = await api.GetStockValue("PETR4");
        Console.WriteLine($"Preço do PETR4: {price}");
    }
}