using SmartLedger.Core.Models;

namespace SmartLedger.Core.Abstractions;

public interface IInvoicePrintService
{
    byte[] RenderThermalReceipt(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity);
    byte[] RenderA4Invoice(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity);
}
