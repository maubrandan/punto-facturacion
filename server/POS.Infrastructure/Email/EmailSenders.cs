using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Contracts.Email;
using POS.Application.Interfaces;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Email;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly EmailOptions _options;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IOptions<EmailOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email (Logging) From={From} To={To} Subject={Subject}\n{Body}",
            _options.FromAddress,
            message.To,
            message.Subject,
            message.PlainTextBody);
        return Task.CompletedTask;
    }
}

public sealed class FileEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<FileEmailSender> _logger;

    public FileEmailSender(IOptions<EmailOptions> options, ILogger<FileEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var dir = string.IsNullOrWhiteSpace(_options.FileDirectory)
            ? Path.Combine(Path.GetTempPath(), "pos-emails")
            : _options.FileDirectory;
        Directory.CreateDirectory(dir);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Sanitize(message.To)}.txt";
        var path = Path.Combine(dir, fileName);
        var content = new StringBuilder()
            .AppendLine($"From: {_options.FromDisplayName} <{_options.FromAddress}>")
            .AppendLine($"To: {message.To}")
            .AppendLine($"Subject: {message.Subject}")
            .AppendLine()
            .AppendLine(message.PlainTextBody)
            .ToString();

        await File.WriteAllTextAsync(path, content, cancellationToken);
        _logger.LogInformation("Email escrito en archivo {Path}", path);
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
    }
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var smtp = _options.Smtp;
        if (string.IsNullOrWhiteSpace(smtp.Host))
            throw new InvalidOperationException("Email:Smtp:Host no está configurado.");

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(smtp.UserName))
        {
            client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password ?? string.Empty);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(
                _options.FromAddress,
                _options.FromDisplayName ?? _options.FromAddress),
            Subject = message.Subject,
            Body = message.HtmlBody ?? message.PlainTextBody,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.HtmlBody)
        };
        mail.To.Add(message.To);
        if (!string.IsNullOrWhiteSpace(message.HtmlBody)
            && !string.IsNullOrWhiteSpace(message.PlainTextBody))
        {
            mail.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(message.PlainTextBody, null, "text/plain"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail, cancellationToken);
        _logger.LogInformation("Email SMTP enviado a {To} Subject={Subject}", message.To, message.Subject);
    }
}

internal static class EmailSenderRegistration
{
    public static IEmailSender Create(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
        var provider = (options.Provider ?? "Logging").Trim();
        return provider.ToLowerInvariant() switch
        {
            "smtp" => ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp),
            "file" => ActivatorUtilities.CreateInstance<FileEmailSender>(sp),
            _ => ActivatorUtilities.CreateInstance<LoggingEmailSender>(sp)
        };
    }
}
