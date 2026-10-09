using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SmartLedger.Core.Models;

namespace SmartLedger.Infrastructure.FinalRelease;

public sealed class ExcelExportService
{
    public void ExportStatement(string path,string businessName,IEnumerable<RecentOperationRow> rows,string currency)
    {
        using var doc=SpreadsheetDocument.Create(path,SpreadsheetDocumentType.Workbook);
        var wb=doc.AddWorkbookPart(); wb.Workbook=new Workbook();
        var styles=wb.AddNewPart<WorkbookStylesPart>(); styles.Stylesheet=CreateStyles(); styles.Stylesheet.Save();
        var ws=wb.AddNewPart<WorksheetPart>(); var data=new SheetData();
        var view=new SheetView{WorkbookViewId=0,RightToLeft=true}; view.Append(new Pane{VerticalSplit=2,TopLeftCell="A4",ActivePane=PaneValues.BottomLeft,State=PaneStateValues.Frozen});
        ws.Worksheet=new Worksheet(new SheetViews(view),data);
        wb.Workbook.AppendChild(new Sheets(new Sheet{Id=wb.GetIdOfPart(ws),SheetId=1,Name="التقرير"}));
        data.Append(SingleRow(businessName,2));
        data.Append(SingleRow($"تاريخ التصدير: {DateTime.Now:yyyy/MM/dd HH:mm}",0));
        data.Append(HeaderRow("التاريخ","الحساب","نوع الحركة","المبلغ", $"البيان ({currency})"));
        foreach(var r in rows.OrderBy(x=>x.Date)) data.Append(MixedRow(r));
        wb.Workbook.Save();
    }

    public void ExportAccountStatement(string path,string businessName,string personName,string bookName,IEnumerable<LedgerOperation> operations,string currency)
    {
        using var doc=SpreadsheetDocument.Create(path,SpreadsheetDocumentType.Workbook);
        var wb=doc.AddWorkbookPart(); wb.Workbook=new Workbook();
        var styles=wb.AddNewPart<WorkbookStylesPart>(); styles.Stylesheet=CreateStyles(); styles.Stylesheet.Save();
        var ws=wb.AddNewPart<WorksheetPart>(); var data=new SheetData();
        var view=new SheetView{WorkbookViewId=0,RightToLeft=true}; view.Append(new Pane{VerticalSplit=4,TopLeftCell="A5",ActivePane=PaneValues.BottomLeft,State=PaneStateValues.Frozen});
        ws.Worksheet=new Worksheet(new SheetViews(view),data);
        wb.Workbook.AppendChild(new Sheets(new Sheet{Id=wb.GetIdOfPart(ws),SheetId=1,Name="كشف الحساب"}));
        var ordered=operations.OrderBy(x=>x.OccurredAt).ToArray(); var balance=0m;
        data.Append(SingleRow(businessName,2));
        data.Append(SingleRow($"كشف حساب: {personName}",2));
        data.Append(SingleRow($"الدفتر: {bookName}  •  تاريخ التصدير: {DateTime.Now:yyyy/MM/dd HH:mm}",0));
        data.Append(HeaderRow("التاريخ","نوع الحركة","البيان","المبلغ",$"الرصيد بعد الحركة ({currency})"));
        foreach(var op in ordered)
        {
            balance+=SignedAmount(op);
            data.Append(AccountRow(op,balance,currency));
        }
        wb.Workbook.Save();
    }

    private static decimal SignedAmount(LedgerOperation op)=>op.Type switch
    {
        OperationType.DebtOnUs or OperationType.Sale or OperationType.Payment or OperationType.Settlement=>op.Amount,
        OperationType.DebtToUs or OperationType.Receive or OperationType.Return or OperationType.Purchase=>-op.Amount,
        _=>0m
    };

    private static Stylesheet CreateStyles()
    {
        var fonts=new Fonts(new Font(),new Font(new Bold()),new Font(new Color{Rgb="FFFFFFFF"},new Bold()));
        var fills=new Fills(new Fill(new PatternFill{PatternType=PatternValues.None}),new Fill(new PatternFill(new ForegroundColor{Rgb="FFE8EEF5"}){PatternType=PatternValues.Solid}));
        var borders=new Borders(new Border());
        var num=new NumberingFormats(new NumberingFormat{NumberFormatId=164,FormatCode="#,##0.00"},new NumberingFormat{NumberFormatId=165,FormatCode="yyyy-mm-dd hh:mm"});
        var formats=new CellFormats(
            new CellFormat(),
            new CellFormat{NumberFormatId=164},
            new CellFormat{FontId=2,FillId=1,Alignment=new Alignment{Horizontal=HorizontalAlignmentValues.Right,Vertical=VerticalAlignmentValues.Center}},
            new CellFormat{NumberFormatId=165}
        );
        return new Stylesheet(num,fonts,fills,borders,new CellStyleFormats(new CellFormat()),formats,new CellStyles(new CellStyle{Name="Normal",FormatId=0,BuiltinId=0}));
    }

    private static Row SingleRow(string value,uint style)=>new(new Cell{DataType=CellValues.InlineString,InlineString=new InlineString(new Text(value)),StyleIndex=style});
    private static Row HeaderRow(params string[] values){var row=new Row();foreach(var v in values)row.Append(new Cell{DataType=CellValues.InlineString,InlineString=new InlineString(new Text(v)),StyleIndex=2});return row;}

    private static Row MixedRow(RecentOperationRow r)
    {
        var row=new Row();
        row.Append(DateCell(r.Date));
        row.Append(TextCell(r.PersonName)); row.Append(TextCell(r.Type)); row.Append(NumberCell(r.Amount)); row.Append(TextCell(r.Note));
        return row;
    }

    private static Row AccountRow(LedgerOperation op,decimal balance,string currency)
    {
        var row=new Row();
        row.Append(DateCell(op.OccurredAt)); row.Append(TextCell(op.TypeLabel)); row.Append(TextCell(op.Note));
        row.Append(NumberCell(op.Amount)); row.Append(NumberCell(balance));
        return row;
    }

    private static Cell DateCell(DateTimeOffset date)=>new(){CellValue=new CellValue(date.LocalDateTime.ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture)),DataType=CellValues.Number,StyleIndex=3};
    private static Cell NumberCell(decimal value)=>new(){CellValue=new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture)),DataType=CellValues.Number,StyleIndex=1};
    private static Cell TextCell(string value)=>new(){DataType=CellValues.InlineString,InlineString=new InlineString(new Text(value??string.Empty))};
}