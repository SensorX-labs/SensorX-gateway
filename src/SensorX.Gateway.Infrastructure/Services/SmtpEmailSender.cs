using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using SensorX.Gateway.Application.Interfaces;

namespace SensorX.Gateway.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = _configuration["Email:SmtpHost"]?.Trim();
        var fromEmail = _configuration["Email:FromEmail"]?.Trim();

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("Email SMTP settings are not configured.");

        var port = _configuration.GetValue("Email:Port", 587);
        var enableSsl = _configuration.GetValue("Email:EnableSsl", true);
        var username = _configuration["Email:Username"]?.Trim();
        var password = _configuration["Email:Password"]?.Replace(" ", string.Empty).Trim();
        var fromName = (_configuration["Email:FromName"] ?? "SensorX").Trim();

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(to);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(username, password);
        }
        else
        {
            client.UseDefaultCredentials = true;
        }

        await client.SendMailAsync(message);
    }
}
