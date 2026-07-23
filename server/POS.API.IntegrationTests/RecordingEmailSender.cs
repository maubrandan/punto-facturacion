using POS.Application.Contracts.Email;
using POS.Application.Interfaces;

namespace POS.API.IntegrationTests;

/// <summary>Fake de correo para asserting en integration tests.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = new();
    private readonly object _gate = new();

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (_gate)
                return _sent.ToList();
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _sent.Add(message);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_gate)
            _sent.Clear();
    }
}
