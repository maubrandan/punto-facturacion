using POS.Application.Contracts.Email;

namespace POS.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
