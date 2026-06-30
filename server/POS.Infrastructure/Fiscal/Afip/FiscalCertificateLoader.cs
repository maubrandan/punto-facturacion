using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Fiscal.Afip;

internal static class FiscalCertificateLoader
{
    public static X509Certificate2 Load(
        string certificateRef,
        string privateKeyRef,
        IOptions<ArcaOptions> options)
    {
        if (string.IsNullOrWhiteSpace(certificateRef))
            throw new InvalidOperationException("Falta referencia de certificado fiscal.");

        var certPath = ResolvePath(certificateRef.Trim(), options.Value.CertificateBasePath);
        if (!File.Exists(certPath))
            throw new FileNotFoundException($"No se encontró el certificado en {certPath}.");

        if (certPath.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
            || certPath.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
        {
            var password = privateKeyRef?.Trim() ?? string.Empty;
            return X509CertificateLoader.LoadPkcs12FromFile(certPath, password);
        }

        if (!string.IsNullOrWhiteSpace(privateKeyRef))
        {
            var keyPath = ResolvePath(privateKeyRef.Trim(), options.Value.CertificateBasePath);
            if (File.Exists(keyPath))
            {
                var certPem = File.ReadAllText(certPath);
                var keyPem = File.ReadAllText(keyPath);
                return X509Certificate2.CreateFromPem(certPem, keyPem);
            }
        }

        return X509CertificateLoader.LoadCertificateFromFile(certPath);
    }

    private static string ResolvePath(string reference, string? basePath)
    {
        var path = reference.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? reference[5..]
            : reference;

        if (Path.IsPathRooted(path))
            return path;

        if (!string.IsNullOrWhiteSpace(basePath))
            return Path.GetFullPath(Path.Combine(basePath, path));

        return Path.GetFullPath(path);
    }
}
