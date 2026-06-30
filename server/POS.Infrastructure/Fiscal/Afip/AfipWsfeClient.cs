using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Interfaces;
using POS.Domain.Billing.Afip;
using POS.Domain.Entities;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Fiscal.Afip;

public sealed class AfipWsfeClient
{
    private readonly IOptions<ArcaOptions> _options;
    private readonly ILogger<AfipWsfeClient> _logger;

    public AfipWsfeClient(IOptions<ArcaOptions> options, ILogger<AfipWsfeClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<(long VoucherNumber, string Cae, DateTime CaeExpiresAtUtc)> AuthorizeAsync(
        FiscalAuthorizationRequest request,
        string token,
        string sign,
        CancellationToken cancellationToken)
    {
        var cbteTipo = AfipComprobanteTypes.ToAfipCode(request.DocumentType);
        var nextNumber = await GetNextVoucherNumberAsync(
            request.TaxId,
            request.PointOfSale,
            cbteTipo,
            token,
            sign,
            cancellationToken);

        var voucherDate = DateTime.UtcNow.AddHours(-3).Date;
        var det = BuildDetRequest(request, nextNumber, voucherDate, cbteTipo);
        var soap = BuildFecaeSoap(request.TaxId, token, sign, request.PointOfSale, cbteTipo, det);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        using var content = new StringContent(soap, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "http://ar.gov.afip.dif.FEV1/FECAESolicitar");
        using var response = await client.PostAsync(_options.Value.WsfeUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("WSFE FECAESolicitar respondió {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"WSFE devolvió {(int)response.StatusCode}.");
        }

        return ParseFecaeResponse(body, nextNumber);
    }

    private async Task<long> GetNextVoucherNumberAsync(
        string taxId,
        int pointOfSale,
        int cbteTipo,
        string token,
        string sign,
        CancellationToken cancellationToken)
    {
        var soap = $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
                      <soap:Body>
                        <ar:FECompUltimoAutorizado>
                          <ar:Auth>
                            <ar:Token>{XmlEscape(token)}</ar:Token>
                            <ar:Sign>{XmlEscape(sign)}</ar:Sign>
                            <ar:Cuit>{taxId}</ar:Cuit>
                          </ar:Auth>
                          <ar:PtoVta>{pointOfSale}</ar:PtoVta>
                          <ar:CbteTipo>{cbteTipo}</ar:CbteTipo>
                        </ar:FECompUltimoAutorizado>
                      </soap:Body>
                    </soap:Envelope>
                    """;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var content = new StringContent(soap, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "http://ar.gov.afip.dif.FEV1/FECompUltimoAutorizado");
        using var response = await client.PostAsync(_options.Value.WsfeUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"WSFE FECompUltimoAutorizado devolvió {(int)response.StatusCode}.");

        var doc = XDocument.Parse(body);
        var cbteNroText = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "CbteNro")?.Value;
        if (!long.TryParse(cbteNroText, out var last))
            last = 0;
        return last + 1;
    }

    private static string BuildDetRequest(
        FiscalAuthorizationRequest request,
        long voucherNumber,
        DateTime voucherDate,
        int cbteTipo)
    {
        var lines = request.Lines;
        var net = lines.Sum(l => l.LineNetSubtotal);
        var tax = lines.Sum(l => l.LineTaxAmount);
        var total = request.TotalAmount;

        var docTipo = 99;
        long docNro = 0;
        if (!string.IsNullOrWhiteSpace(request.BuyerTaxId)
            && long.TryParse(new string(request.BuyerTaxId.Where(char.IsDigit).ToArray()), out var buyerCuit))
        {
            docTipo = 80;
            docNro = buyerCuit;
        }

        var condicionIva = docTipo == 80 ? 1 : 5;

        var ivaGroups = lines
            .GroupBy(l => AfipVatAlicuotaMapper.ToAfipAlicuotaId(l.TaxRate))
            .Select(
                g => new
                {
                    Id = g.Key,
                    BaseImp = g.Sum(x => x.LineNetSubtotal),
                    Importe = g.Sum(x => x.LineTaxAmount)
                })
            .Where(x => x.BaseImp != 0 || x.Importe != 0)
            .ToList();

        var ivaXml = ivaGroups.Count == 0
            ? string.Empty
            : "<ar:Iva>" + string.Join(
                string.Empty,
                ivaGroups.Select(
                    g => $"""
                          <ar:AlicIva>
                            <ar:Id>{g.Id}</ar:Id>
                            <ar:BaseImp>{g.BaseImp.ToString("F2", CultureInfo.InvariantCulture)}</ar:BaseImp>
                            <ar:Importe>{g.Importe.ToString("F2", CultureInfo.InvariantCulture)}</ar:Importe>
                          </ar:AlicIva>
                          """)) + "</ar:Iva>";

        var asocXml = string.Empty;
        if (request.OriginalVoucherNumber.HasValue && cbteTipo is 3 or 8)
        {
            var origTipo = cbteTipo == 3 ? 1 : 6;
            asocXml = $"""
                       <ar:CbtesAsoc>
                         <ar:CbteAsoc>
                           <ar:Tipo>{origTipo}</ar:Tipo>
                           <ar:PtoVta>{request.PointOfSale}</ar:PtoVta>
                           <ar:Nro>{request.OriginalVoucherNumber.Value}</ar:Nro>
                         </ar:CbteAsoc>
                       </ar:CbtesAsoc>
                       """;
        }

        return $"""
                <ar:FECAEDetRequest>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>{docTipo}</ar:DocTipo>
                  <ar:DocNro>{docNro}</ar:DocNro>
                  <ar:CbteDesde>{voucherNumber}</ar:CbteDesde>
                  <ar:CbteHasta>{voucherNumber}</ar:CbteHasta>
                  <ar:CbteFch>{voucherDate:yyyyMMdd}</ar:CbteFch>
                  <ar:ImpTotal>{total.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpTotal>
                  <ar:ImpTotConc>0.00</ar:ImpTotConc>
                  <ar:ImpNeto>{net.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpNeto>
                  <ar:ImpOpEx>0.00</ar:ImpOpEx>
                  <ar:ImpTrib>0.00</ar:ImpTrib>
                  <ar:ImpIVA>{tax.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpIVA>
                  <ar:FchServDesde></ar:FchServDesde>
                  <ar:FchServHasta></ar:FchServHasta>
                  <ar:FchVtoPago></ar:FchVtoPago>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>{condicionIva}</ar:CondicionIVAReceptorId>
                  {asocXml}
                  {ivaXml}
                </ar:FECAEDetRequest>
                """;
    }

    private static string BuildFecaeSoap(
        string taxId,
        string token,
        string sign,
        int pointOfSale,
        int cbteTipo,
        string detXml)
    {
        return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
                  <soap:Body>
                    <ar:FECAESolicitar>
                      <ar:Auth>
                        <ar:Token>{XmlEscape(token)}</ar:Token>
                        <ar:Sign>{XmlEscape(sign)}</ar:Sign>
                        <ar:Cuit>{taxId}</ar:Cuit>
                      </ar:Auth>
                      <ar:FeCAEReq>
                        <ar:FeCabReq>
                          <ar:CantReg>1</ar:CantReg>
                          <ar:PtoVta>{pointOfSale}</ar:PtoVta>
                          <ar:CbteTipo>{cbteTipo}</ar:CbteTipo>
                        </ar:FeCabReq>
                        <ar:FeDetReq>
                          {detXml}
                        </ar:FeDetReq>
                      </ar:FeCAEReq>
                    </ar:FECAESolicitar>
                  </soap:Body>
                </soap:Envelope>
                """;
    }

    private static (long VoucherNumber, string Cae, DateTime CaeExpiresAtUtc) ParseFecaeResponse(
        string soapResponse,
        long expectedVoucher)
    {
        var doc = XDocument.Parse(soapResponse);
        var resultado = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Resultado")?.Value;
        if (!string.Equals(resultado, "A", StringComparison.OrdinalIgnoreCase))
        {
            var errors = doc.Descendants()
                .Where(e => e.Name.LocalName == "Err")
                .Select(
                    e =>
                    {
                        var code = e.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value ?? "?";
                        var msg = e.Elements().FirstOrDefault(x => x.Name.LocalName == "Msg")?.Value ?? "";
                        return $"{code}: {msg}";
                    })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            var obs = doc.Descendants()
                .Where(e => e.Name.LocalName == "Obs")
                .Select(e => e.Element("Msg")?.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            var message = errors.Count > 0
                ? string.Join("; ", errors)
                : obs.Count > 0
                    ? string.Join("; ", obs!)
                    : "ARCA rechazó el comprobante.";
            throw new InvalidOperationException(message);
        }

        var cae = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "CAE")?.Value;
        var caeVto = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "CAEFchVto")?.Value;
        if (string.IsNullOrWhiteSpace(cae) || string.IsNullOrWhiteSpace(caeVto))
            throw new InvalidOperationException("WSFE no devolvió CAE.");

        var expires = DateTime.ParseExact(caeVto, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        return (expectedVoucher, cae, expires.ToUniversalTime());
    }

    private static string XmlEscape(string value) => WebUtility.HtmlEncode(value);
}
