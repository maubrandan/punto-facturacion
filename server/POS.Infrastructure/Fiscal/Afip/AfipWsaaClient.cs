using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Fiscal.Afip;

public sealed class AfipWsaaClient
{
    private static readonly ConcurrentDictionary<string, CachedCredentials> TokenCache = new();

    private readonly IOptions<ArcaOptions> _options;
    private readonly ILogger<AfipWsaaClient> _logger;

    public AfipWsaaClient(IOptions<ArcaOptions> options, ILogger<AfipWsaaClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<(string Token, string Sign)> GetCredentialsAsync(
        string taxId,
        X509Certificate2 certificate,
        bool isProduction,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{taxId}:{_options.Value.AfipServiceName}:{isProduction}";
        if (TokenCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(2))
            return (cached.Token, cached.Sign);

        var tra = BuildTraXml();
        var cms = SignTra(tra, certificate);
        var soap = BuildLoginSoap(cms, isProduction);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var content = new StringContent(soap, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "\"\"");
        using var response = await client.PostAsync(_options.Value.WsaaUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("WSAA respondió {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"WSAA devolvió {(int)response.StatusCode}.");
        }

        var credentialsXml = ExtractLoginReturn(body);
        var doc = XDocument.Parse(credentialsXml);
        var token = doc.Root?.Element("credentials")?.Element("token")?.Value;
        var sign = doc.Root?.Element("credentials")?.Element("sign")?.Value;
        var expirationText = doc.Root?.Element("header")?.Element("expirationTime")?.Value;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sign))
            throw new InvalidOperationException("WSAA no devolvió token/sign válidos.");

        var expiresAt = DateTime.UtcNow.AddHours(10);
        if (!string.IsNullOrWhiteSpace(expirationText)
            && DateTime.TryParse(expirationText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            expiresAt = parsed.ToUniversalTime();
        }

        TokenCache[cacheKey] = new CachedCredentials(token, sign, expiresAt);
        return (token, sign);
    }

    private string BuildTraXml()
    {
        var now = DateTime.UtcNow;
        var uniqueId = (uint)now.Ticks;
        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <loginTicketRequest version="1.0">
                  <header>
                    <uniqueId>{uniqueId}</uniqueId>
                    <generationTime>{now.AddMinutes(-5):yyyy-MM-ddTHH:mm:ssZ}</generationTime>
                    <expirationTime>{now.AddMinutes(5):yyyy-MM-ddTHH:mm:ssZ}</expirationTime>
                  </header>
                  <service>{_options.Value.AfipServiceName}</service>
                </loginTicketRequest>
                """;
    }

    private static string SignTra(string traXml, X509Certificate2 certificate)
    {
        var bytes = Encoding.UTF8.GetBytes(traXml);
        var contentInfo = new ContentInfo(bytes);
        var signedCms = new SignedCms(contentInfo);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };
        signedCms.ComputeSignature(signer);
        return Convert.ToBase64String(signedCms.Encode());
    }

    private static string BuildLoginSoap(string cmsBase64, bool isProduction)
    {
        var ns = isProduction
            ? "https://wsaa.afip.gov.ar/ws/services/LoginCms"
            : "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";
        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:wsaa="{ns}">
                  <soapenv:Header/>
                  <soapenv:Body>
                    <wsaa:loginCms>
                      <wsaa:in0>{cmsBase64}</wsaa:in0>
                    </wsaa:loginCms>
                  </soapenv:Body>
                </soapenv:Envelope>
                """;
    }

    private static string ExtractLoginReturn(string soapResponse)
    {
        var doc = XDocument.Parse(soapResponse);
        var loginReturn = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "loginCmsReturn")?.Value;
        if (string.IsNullOrWhiteSpace(loginReturn))
            throw new InvalidOperationException("Respuesta WSAA sin loginCmsReturn.");
        return loginReturn;
    }

    private sealed record CachedCredentials(string Token, string Sign, DateTime ExpiresAtUtc);
}
