using System.Net;
using Microsoft.Extensions.Options;
using POS.Application.Contracts.Email;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Email;

internal static class AuthEmailComposer
{
    public static EmailMessage PasswordReset(
        EmailOptions options,
        string email,
        string token)
    {
        var encodedToken = WebUtility.UrlEncode(token);
        var encodedEmail = WebUtility.UrlEncode(email);
        var baseUrl = (options.PublicAppBaseUrl ?? "http://localhost:4200").TrimEnd('/');
        var link = $"{baseUrl}/reset-password?email={encodedEmail}&token={encodedToken}";

        var body =
            $"Recibimos una solicitud para restablecer la contraseña de {email}.{Environment.NewLine}{Environment.NewLine}" +
            $"Abrí este enlace (o copiá el token en la pantalla de restablecimiento):{Environment.NewLine}" +
            $"{link}{Environment.NewLine}{Environment.NewLine}" +
            $"Token:{Environment.NewLine}{token}{Environment.NewLine}{Environment.NewLine}" +
            "Si no solicitaste este cambio, ignorá este mensaje.";

        return new EmailMessage(
            email,
            "Restablecer contraseña — Punto Facturacion",
            body);
    }

    public static EmailMessage EmailConfirmation(
        EmailOptions options,
        string email,
        string token)
    {
        var encodedToken = WebUtility.UrlEncode(token);
        var encodedEmail = WebUtility.UrlEncode(email);
        var baseUrl = (options.PublicAppBaseUrl ?? "http://localhost:4200").TrimEnd('/');
        var link = $"{baseUrl}/confirm-email?email={encodedEmail}&token={encodedToken}";

        var body =
            $"Confirmá el correo {email} para tu cuenta de Punto Facturacion.{Environment.NewLine}{Environment.NewLine}" +
            $"Abrí este enlace:{Environment.NewLine}" +
            $"{link}{Environment.NewLine}{Environment.NewLine}" +
            $"Token:{Environment.NewLine}{token}{Environment.NewLine}";

        return new EmailMessage(
            email,
            "Confirmá tu correo — Punto Facturacion",
            body);
    }
}
