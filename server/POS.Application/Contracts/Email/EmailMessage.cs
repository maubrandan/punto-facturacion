namespace POS.Application.Contracts.Email;

public sealed record EmailMessage(
    string To,
    string Subject,
    string PlainTextBody,
    string? HtmlBody = null);
