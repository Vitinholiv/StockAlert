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

    public async Task SendEmail(string subject, string htmlBody) {
        using var smtpClient = new SmtpClient(_host, _port)
        {
            Credentials = new NetworkCredential(_user, _pass),
            EnableSsl = true
        };

        using var message = new MailMessage(_sender, _recipient, subject, htmlBody)
        {
            IsBodyHtml = true
        };
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
    private bool _sleeping = false;
    public bool Sleep 
    { 
        get => _sleeping; 
        set {
            if (_sleeping != value) {
                _sleeping = value;
                if (_sleeping) {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Mercado fechado. Entrando em modo de espera.");
                } else {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Mercado aberto. Retomando monitoramento.");
                }
            }
        }
    }

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
            string subject = $"[ALERTA DE VENDA] {asset} a R$ {price:F2}";
            string body = GenerateAlertEmailHtml(asset, "VENDA", price, _sellPrice, now, "#10b981", "superou o preço teto de venda");
            await emailManager.SendEmail(subject, body);
            _lastSellAlert = now;
            Console.WriteLine($"-> E-mail de venda enviado. Cooldown de venda ativo até {now + _cooldown:HH:mm:ss}.");
        }
        else if (Status == -1 && (_lastBuyAlert == null || now - _lastBuyAlert > _cooldown)) {
            string subject = $"[ALERTA DE COMPRA] {asset} a R$ {price:F2}";
            string body = GenerateAlertEmailHtml(asset, "COMPRA", price, _buyPrice, now, "#ef4444", "caiu abaixo do preço piso de compra");
            await emailManager.SendEmail(subject, body);
            _lastBuyAlert = now;
            Console.WriteLine($"-> E-mail de compra enviado. Cooldown de compra ativo até {now + _cooldown:HH:mm:ss}.");
        }
    }

    private static string GenerateAlertEmailHtml(string asset, string type, decimal currentPrice, decimal targetPrice, DateTime time, string colorHex, string reason) {
        return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset=""UTF-8"">
        </head>
        <body style=""margin: 0; padding: 20px; font-family: 'Segoe UI', Arial, sans-serif; background-color: #f8fafc; color: #1e293b;"">
            <div style=""max-width: 540px; margin: 0 auto; background-color: #ffffff; border-radius: 10px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05);"">
                <div style=""background-color: {colorHex}; padding: 20px; text-align: center;"">
                    <span style=""background-color: rgba(255,255,255,0.25); color: #ffffff; padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: bold; text-transform: uppercase; letter-spacing: 1px;"">Alerta de {type}</span>
                    <h1 style=""color: #ffffff; margin: 10px 0 0 0; font-size: 24px;"">{asset} atingiu R$ {currentPrice:F2}</h1>
                </div>
                <div style=""padding: 24px;"">
                    <p style=""font-size: 15px; line-height: 1.5; color: #475569; margin: 0 0 20px 0;"">
                        A cotação do ativo <strong>{asset}</strong> {reason} configurado.
                    </p>
                    <table style=""width: 100%; border-collapse: collapse; margin-bottom: 20px;"">
                        <tr style=""border-bottom: 1px solid #f1f5f9;"">
                            <td style=""padding: 10px 0; color: #64748b; font-size: 14px;"">Preço Atual:</td>
                            <td style=""padding: 10px 0; font-weight: bold; font-size: 16px; text-align: right; color: {colorHex};"">R$ {currentPrice:F2}</td>
                        </tr>
                        <tr style=""border-bottom: 1px solid #f1f5f9;"">
                            <td style=""padding: 10px 0; color: #64748b; font-size: 14px;"">Preço Limite ({type}):</td>
                            <td style=""padding: 10px 0; font-weight: bold; font-size: 14px; text-align: right; color: #1e293b;"">R$ {targetPrice:F2}</td>
                        </tr>
                        <tr>
                            <td style=""padding: 10px 0; color: #64748b; font-size: 14px;"">Horário do Disparo:</td>
                            <td style=""padding: 10px 0; font-size: 14px; text-align: right; color: #64748b;"">{time:dd/MM/yyyy HH:mm:ss}</td>
                        </tr>
                    </table>
                    <div style=""background-color: #f8fafc; border-left: 4px solid {colorHex}; padding: 12px 16px; border-radius: 4px; font-size: 13px; color: #475569;"">
                        💡 <strong>Sugestão:</strong> Considere executar uma ordem de <strong>{type}</strong> para o ativo {asset} na sua corretora.
                    </div>
                </div>
                <div style=""background-color: #f8fafc; padding: 14px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #f1f5f9;"">
                    StockQuoteAlert • Monitoramento em tempo real
                </div>
            </div>
        </body>
        </html>";
    }
}

public class ChartService {
    public static void SaveRecord(string asset, DateTime timestamp, decimal price, decimal buyPrice, decimal sellPrice, int status) {
        Directory.CreateDirectory("data");
        string filename = Path.Combine("data", $"history_{asset}_{timestamp:yyyy-MM-dd}.csv");
        bool exists = File.Exists(filename);
        using var writer = new StreamWriter(filename, append: true);
        if (!exists) {
            writer.WriteLine("Timestamp,Preco,LimCompra,LimVenda,Status");
        }
        writer.WriteLine($"{timestamp:yyyy-MM-dd HH:mm:ss},{price.ToString(CultureInfo.InvariantCulture)},{buyPrice.ToString(CultureInfo.InvariantCulture)},{sellPrice.ToString(CultureInfo.InvariantCulture)},{status}");
    }

    public static void GeneratePendingCharts(string asset) {
        if (!Directory.Exists("data")) return;

        var files = Directory.GetFiles("data", $"history_{asset}_*.csv");
        foreach (var csvFile in files) {
            string filenameWithoutExt = Path.GetFileNameWithoutExtension(csvFile);
            string dateStr = filenameWithoutExt.Replace($"history_{asset}_", "");
            string htmlFile = Path.Combine("data", $"plot_{asset}_{dateStr}.html");

            if (!File.Exists(htmlFile)) {
                try {
                    GenerateHtmlChart(csvFile, htmlFile, asset, dateStr);
                    Console.WriteLine($"[Gráfico] Gerado com sucesso: {htmlFile}");
                }
                catch (Exception ex) {
                    Console.WriteLine($"[Gráfico] Erro ao gerar {htmlFile}: {ex.Message}");
                }
            }
        }
    }

    private static void GenerateHtmlChart(string csvPath, string htmlPath, string asset, string dateStr) {
        var lines = File.ReadAllLines(csvPath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0) return;

        var labels = new List<string>();
        var prices = new List<string>();
        var buyLimits = new List<string>();
        var sellLimits = new List<string>();
        var pointRadii = new List<int>();
        var pointColors = new List<string>();

        decimal minPrice = decimal.MaxValue;
        decimal maxPrice = decimal.MinValue;
        decimal lastPrice = 0;
        decimal limCompra = 0;
        decimal limVenda = 0;

        DateTime? prevTime = null;

        foreach (var line in lines) {
            var parts = line.Split(',');
            if (parts.Length < 5) continue;

            DateTime time = DateTime.ParseExact(parts[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            decimal price = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
            limCompra = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
            limVenda = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
            int status = int.Parse(parts[4]);

            if (prevTime != null && (time - prevTime.Value).TotalMinutes > 5) {
                labels.Add($"\"{prevTime.Value.AddMinutes(1):HH:mm}\"");
                prices.Add("null");
                buyLimits.Add(limCompra.ToString(CultureInfo.InvariantCulture));
                sellLimits.Add(limVenda.ToString(CultureInfo.InvariantCulture));
                pointRadii.Add(0);
                pointColors.Add("\"#3b82f6\"");
            }

            labels.Add($"\"{time:HH:mm:ss}\"");
            prices.Add(price.ToString(CultureInfo.InvariantCulture));
            buyLimits.Add(limCompra.ToString(CultureInfo.InvariantCulture));
            sellLimits.Add(limVenda.ToString(CultureInfo.InvariantCulture));

            if (status == 1) {
                pointRadii.Add(8);
                pointColors.Add("\"#10b981\"");
            } else if (status == -1) {
                pointRadii.Add(8);
                pointColors.Add("\"#ef4444\"");
            } else {
                pointRadii.Add(2);
                pointColors.Add("\"#3b82f6\"");
            }

            if (price < minPrice) minPrice = price;
            if (price > maxPrice) maxPrice = price;
            lastPrice = price;
            prevTime = time;
        }

        string htmlContent = $@"<!DOCTYPE html>
        <html lang=""pt-BR"">
        <head>
            <meta charset=""UTF-8"">
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
            <title>Histórico {asset} - {dateStr}</title>
            <script src=""https://cdn.jsdelivr.net/npm/chart.js""></script>
            <style>
                * {{ margin: 0; padding: 0; box-sizing: border-box; font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; }}
                body {{ background-color: #0f172a; color: #f8fafc; padding: 30px 20px; }}
                .container {{ max-width: 1100px; margin: 0 auto; }}
                .header {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }}
                .title {{ font-size: 26px; font-weight: 700; color: #60a5fa; }}
                .date {{ font-size: 16px; color: #94a3b8; }}
                .cards {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 24px; }}
                .card {{ background-color: #1e293b; padding: 18px; border-radius: 12px; border: 1px solid #334155; }}
                .card-label {{ font-size: 12px; color: #94a3b8; text-transform: uppercase; margin-bottom: 6px; }}
                .card-value {{ font-size: 20px; font-weight: 700; color: #f8fafc; }}
                .card-buy {{ color: #ef4444; }}
                .card-sell {{ color: #10b981; }}
                .chart-box {{ background-color: #1e293b; padding: 24px; border-radius: 14px; border: 1px solid #334155; box-shadow: 0 10px 25px rgba(0,0,0,0.3); }}
            </style>
        </head>
        <body>
            <div class=""container"">
                <div class=""header"">
                    <div>
                        <h1 class=""title"">Monitoramento {asset}</h1>
                        <div class=""date"">Relatório Diário - {dateStr}</div>
                    </div>
                </div>

                <div class=""cards"">
                    <div class=""card"">
                        <div class=""card-label"">Último Preço</div>
                        <div class=""card-value"">R$ {lastPrice:F2}</div>
                    </div>
                    <div class=""card"">
                        <div class=""card-label"">Mínima do Dia</div>
                        <div class=""card-value"">R$ {minPrice:F2}</div>
                    </div>
                    <div class=""card"">
                        <div class=""card-label"">Máxima do Dia</div>
                        <div class=""card-value"">R$ {maxPrice:F2}</div>
                    </div>
                    <div class=""card"">
                        <div class=""card-label"">Limiar de Compra</div>
                        <div class=""card-value card-buy"">R$ {limCompra:F2}</div>
                    </div>
                    <div class=""card"">
                        <div class=""card-label"">Limiar de Venda</div>
                        <div class=""card-value card-sell"">R$ {limVenda:F2}</div>
                    </div>
                </div>

                <div class=""chart-box"">
                    <canvas id=""stockChart"" height=""120""></canvas>
                </div>
            </div>

            <script>
                const ctx = document.getElementById('stockChart').getContext('2d');
                new Chart(ctx, {{
                    type: 'line',
                    data: {{
                        labels: [{string.Join(",", labels)}],
                        datasets: [
                            {{
                                label: 'Preço {asset}',
                                data: [{string.Join(",", prices)}],
                                borderColor: '#3b82f6',
                                backgroundColor: 'rgba(59, 130, 246, 0.08)',
                                borderWidth: 2.5,
                                spanGaps: false,
                                pointRadius: [{string.Join(",", pointRadii)}],
                                pointBackgroundColor: [{string.Join(",", pointColors)}],
                                tension: 0.1
                            }},
                            {{
                                label: 'Limiar de Venda',
                                data: [{string.Join(",", sellLimits)}],
                                borderColor: '#10b981',
                                borderWidth: 2,
                                borderDash: [6, 6],
                                pointRadius: 0,
                                fill: false
                            }},
                            {{
                                label: 'Limiar de Compra',
                                data: [{string.Join(",", buyLimits)}],
                                borderColor: '#ef4444',
                                borderWidth: 2,
                                borderDash: [6, 6],
                                pointRadius: 0,
                                fill: false
                            }}
                        ]
                    }},
                    options: {{
                        responsive: true,
                        plugins: {{
                            legend: {{ labels: {{ color: '#cbd5e1', font: {{ size: 13 }} }} }},
                            tooltip: {{
                                backgroundColor: '#0f172a',
                                titleColor: '#60a5fa',
                                bodyColor: '#f8fafc',
                                borderColor: '#334155',
                                borderWidth: 1,
                                padding: 10
                            }}
                        }},
                        scales: {{
                            x: {{ grid: {{ color: '#334155' }}, ticks: {{ color: '#94a3b8' }} }},
                            y: {{ grid: {{ color: '#334155' }}, ticks: {{ color: '#94a3b8' }} }}
                        }}
                    }}
                }});
            </script>
        </body>
        </html>";
        File.WriteAllText(htmlPath, htmlContent);
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

        while (true) {
            try {
                DateTime now = DateTime.Now;

                bool isWeekday = now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday;
                bool isMarketOpen = isWeekday && now.Hour >= 10 && now.Hour < 17;

                controller.Sleep = !isMarketOpen;
                if (controller.Sleep) {
                    ChartService.GeneratePendingCharts(asset);
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    continue;
                }

                decimal price = await api.GetStockValue(asset);
                Console.WriteLine($"[{now:HH:mm:ss}] {asset}: R$ {price:F2}");

                await controller.ProcessPrice(asset, price, emailManager);
                ChartService.SaveRecord(asset, now, price, buyPrice, sellPrice, controller.Status);
            }
            catch (Exception ex) {
                Console.WriteLine($"[Erro] {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromMinutes(1));
        }
    }
}