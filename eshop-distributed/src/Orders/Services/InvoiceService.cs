using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Orders.Services;

public class InvoiceService
{
    public byte[] GenerateInvoicePdf(Order order)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text("eShop").FontSize(20).Bold();
                    column.Item().Text($"Invoice #{order.Id}").FontSize(14);
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Line($"Billed to: {order.UserName}");
                            text.Line($"Order date: {order.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC");
                            text.Line($"Paid on: {order.PaidAtUtc:yyyy-MM-dd HH:mm} UTC");
                        });
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Product").Bold();
                            header.Cell().Text("Price").Bold();
                            header.Cell().Text("Qty").Bold();
                            header.Cell().Text("Subtotal").Bold();
                            header.Cell().ColumnSpan(4).PaddingTop(2).BorderBottom(1);
                        });

                        foreach (var item in order.Items)
                        {
                            table.Cell().Text(item.ProductName);
                            table.Cell().Text(item.Price.ToString("C2"));
                            table.Cell().Text(item.Quantity.ToString());
                            table.Cell().Text((item.Price * item.Quantity).ToString("C2"));
                        }
                    });

                    column.Item().AlignRight().Text($"Total: {order.TotalAmount:C2}").FontSize(14).Bold();
                });

                page.Footer().AlignCenter().Text("Thank you for shopping with eShop.");
            });
        });

        return document.GeneratePdf();
    }
}
