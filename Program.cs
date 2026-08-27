using System.Text.Json.Nodes;
using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Net;

public class ApiManager {
    private static readonly HttpClient _client = new();
    private readonly string _token;

    public List<decimal> PriceHistory { get; private set; }

    public ApiManager(string token){
        _token = token;
        PriceHistory = [];
    }

    public async Task<decimal> GetStockValue(string asset) {
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

public class EmailManager {
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _pass;
    private readonly string _sender;
    private readonly string _recipient;

    public EmailManager(string host, int port, string user, string pass, string sender, string recipient) {
        _host = host;
        _port = port;
        _user = user;
        _pass = pass;
        _sender = sender;
        _recipient = recipient;
    }

    public async Task SendEmail(string subject, string body) {
        using var smtpClient = new SmtpClient(_host, _port)
        {
            Credentials = new NetworkCredential(_user, _pass),
            EnableSsl = true
        };

        using var message = new MailMessage(_sender, _recipient, subject, body);
        await smtpClient.SendMailAsync(message);
    }
}

public class StockController {
    private readonly decimal _sellPrice;
    private readonly decimal _buyPrice;
    private readonly TimeSpan _cooldown;
    private DateTime? _lastSellAlert;
    private DateTime? _lastBuyAlert;

    public int Status { get; private set; } = 0;

    public StockController(decimal sellPrice, decimal buyPrice, int cooldownMinutes = 10) {
        _sellPrice = sellPrice;
        _buyPrice = buyPrice;
        _cooldown = TimeSpan.FromMinutes(cooldownMinutes);
    }

    public async Task ProcessPrice(string asset, decimal price, EmailManager emailManager) {
        Status = 0;
        if (price > _sellPrice) Status = 1;
        else if (price < _buyPrice) Status = -1;

        DateTime now = DateTime.Now;

        if (Status == 1 && (_lastSellAlert == null || now - _lastSellAlert > _cooldown)) {
            await emailManager.SendEmail(
                $"[ALERTA DE VENDA] {asset} a R$ {price:F2}",
                $"O ativo {asset} atingiu R$ {price:F2}, superando o preço de venda de R$ {_sellPrice:F2}."
            );
            _lastSellAlert = now;
            Console.WriteLine($"-> E-mail de venda enviado. Cooldown de venda ativo até {now + _cooldown:HH:mm:ss}.");
        }
        else if (Status == -1 && (_lastBuyAlert == null || now - _lastBuyAlert > _cooldown)) {
            await emailManager.SendEmail(
                $"[ALERTA DE COMPRA] {asset} a R$ {price:F2}",
                $"O ativo {asset} atingiu R$ {price:F2}, caindo abaixo do preço de compra de R$ {_buyPrice:F2}."
            );
            _lastBuyAlert = now;
            Console.WriteLine($"-> E-mail de compra enviado. Cooldown de compra ativo até {now + _cooldown:HH:mm:ss}.");
        }
    }
}

class Program {
    static async Task Main(string[] args) {
        if (args.Length < 3) {
            Console.WriteLine("Uso Incorreto. Tente: stock-quote-alert.exe <ATIVO> <PRECO_VENDA> <PRECO_COMPRA>");
            return;
        }

        string asset = args[0].ToUpper();
        decimal sellPrice, buyPrice;
        bool validSell = decimal.TryParse(args[1], NumberStyles.Number, CultureInfo.InvariantCulture, out sellPrice);
        bool validBuy = decimal.TryParse(args[2], NumberStyles.Number, CultureInfo.InvariantCulture, out buyPrice);

        if (!validSell || !validBuy) {
            Console.WriteLine("Erro no formato dos valores de compra e venda. Garanta que são números decimais com duas casas, separando casas decimais por ponto.");
            return;
        }

        var config = JsonNode.Parse(File.ReadAllText("config.json"));

        int port = (int)config?["SmtpPort"]!;
        string host = (string)config?["SmtpHost"]!;
        string user = (string)config?["SmtpUser"]!;
        string pass = (string)config?["SmtpPass"]!;
        string sender = (string)config?["SenderEmail"]!;
        string recipient = (string)config?["RecipientEmail"]!;
        string token = (string)config?["ApiToken"]!;

        var api = new ApiManager(token);
        var emailManager = new EmailManager(host, port, user, pass, sender, recipient);
        var controller = new StockController(sellPrice, buyPrice);

        Console.WriteLine($"Iniciando monitoramento de {asset} (Venda: >= {sellPrice:F2} | Compra: <= {buyPrice:F2})\n");
        while (true) {
            try {
                decimal price = await api.GetStockValue(asset);
                DateTime now = DateTime.Now;

                Console.WriteLine($"[{now:HH:mm:ss}] {asset}: R$ {price:F2}");
                await controller.ProcessPrice(asset, price, emailManager);
            }
            catch (Exception ex) {
                Console.WriteLine($"[Erro] {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromMinutes(1));
        }
    }
}