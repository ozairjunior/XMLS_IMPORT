using XmlFiscal.Domain;

namespace XmlFiscal.Application;

public sealed class PartyResolver : IPartyResolver
{
    public PartyResolution Resolve(IReadOnlyCollection<Company> companies, ParsedParty issuer, ParsedParty recipient, Guid? preferredCompanyId = null)
    {
        var issuerId = TaxIdNormalizer.Normalize(issuer.TaxId);
        var recipientId = TaxIdNormalizer.Normalize(recipient.TaxId);
        var active = companies.Where(c => c.IsActive).ToArray();
        if (preferredCompanyId is not null)
        {
            var preferred = active.FirstOrDefault(c => c.Id == preferredCompanyId);
            if (preferred is not null)
            {
                var id = TaxIdNormalizer.Normalize(preferred.TaxId);
                if (id == issuerId && id == recipientId) return new(preferred, DocumentDirection.Unknown, true, "Empresa aparece como emitente e destinatária.");
                if (id == issuerId) return new(preferred, DocumentDirection.Outbound, false, "CNPJ configurado encontrado no emitente.");
                if (id == recipientId) return new(preferred, DocumentDirection.Inbound, false, "CNPJ configurado encontrado no destinatário.");
            }
        }
        var issuerCompany = active.FirstOrDefault(c => TaxIdNormalizer.Normalize(c.TaxId) == issuerId);
        var recipientCompany = active.FirstOrDefault(c => TaxIdNormalizer.Normalize(c.TaxId) == recipientId);
        if (issuerCompany is not null && recipientCompany is not null)
            return new(issuerCompany, DocumentDirection.Outbound, true, "Documento entre empresas configuradas; associado ao emitente.");
        if (issuerCompany is not null) return new(issuerCompany, DocumentDirection.Outbound, false, "Empresa configurada encontrada no emitente.");
        if (recipientCompany is not null) return new(recipientCompany, DocumentDirection.Inbound, false, "Empresa configurada encontrada no destinatário.");
        return new(null, DocumentDirection.Unknown, false, "Nenhum CNPJ configurado foi encontrado no documento.");
    }
}
