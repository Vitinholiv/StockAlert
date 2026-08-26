using System.Net;
using System.Net.Mail;
using System.Text.Json.Nodes;

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

class Program {
    static async Task Main(string[] args){
        var config = JsonNode.Parse(File.ReadAllText("config.json"));

        int port = (int)config?["SmtpPort"]!;
        string host = (string)config?["SmtpHost"]!;
        string user = (string)config?["SmtpUser"]!;
        string pass = (string)config?["SmtpPass"]!;
        string sender = (string)config?["SenderEmail"]!;
        string recipient = (string)config?["RecipientEmail"]!;

        var emailManager = new EmailManager(host, port, user, pass, sender, recipient);

        Console.WriteLine($"Teste: Envio de email para {recipient}...");
        await emailManager.SendEmail("Teste", "Email Inicial");
    }
}