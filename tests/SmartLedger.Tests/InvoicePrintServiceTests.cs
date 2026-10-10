using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Reporting;
using Xunit;

namespace SmartLedger.Tests;

public sealed class InvoicePrintServiceTests
{
    [Fact]
    public void ThermalReceipt_RendersValidPdfWithArabicInvoiceContent()
    {
        var invoice=new Invoice(Guid.NewGuid(),InvoiceType.Sale,null,DateTimeOffset.UtcNow,125m,5m,120m,120m,PaymentMode.Cash,"اختبار",Guid.NewGuid())
        {
            InvoiceNumber="INV-2026-0001",CustomerName="عميل نقدي",Currency="ر.ي"
        };
        var lines=new[]{new InvoiceLine(Guid.NewGuid(),invoice.Id,Guid.NewGuid(),"صنف عربي",2m,"حبة",62.5m,5m,120m){QuantityMinor=2m}};
        var identity=new BusinessIdentity("متجر الاختبار","","7770000","صنعاء","ر.ي");
        var pdf=new InvoicePrintService().RenderThermalReceipt(invoice,lines,identity);
        Assert.True(pdf.Length>500);
        Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(pdf,0,5));
    }

    [Fact]
    public void A4Invoice_RendersValidPdf()
    {
        var invoice=new Invoice(Guid.NewGuid(),InvoiceType.Sale,null,DateTimeOffset.UtcNow,20m,0m,20m,0m,PaymentMode.Cash,"",Guid.NewGuid())
        {
            InvoiceNumber="INV-2026-0002",Currency="ر.ي"
        };
        var lines=new[]{new InvoiceLine(Guid.NewGuid(),invoice.Id,Guid.NewGuid(),"منتج",1m,"حبة",20m,0m,20m)};
        var pdf=new InvoicePrintService().RenderA4Invoice(invoice,lines,new BusinessIdentity("المتجر","","","","ر.ي"));
        Assert.True(pdf.Length>500);
        Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(pdf,0,5));
    }
}
