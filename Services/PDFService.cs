using Microsoft.Extensions.Options;
using BatchBaker.Configuration;
using BatchBaker.Models;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BatchBaker.Services
{
    public class PDFService
    {
        private readonly string _filePath;

        public PDFService(IOptions<PDFSettings> pdfSettings)
        {
            _filePath = Environment.ExpandEnvironmentVariables(pdfSettings.Value.FilePath);
        }
        
        public string GeneratePDF(People person)
        {
            if (!Directory.Exists(_filePath))
            {
                Directory.CreateDirectory(_filePath);
            }
            string pdfFilePath = Path.Combine(_filePath, $"{person.Username}_credentials.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(20));
                    page.Header()
                        .Text("Your New Account Credentials")
                        .SemiBold().FontSize(36).FontColor(Colors.Blue.Medium);
                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Spacing(20);
                            x.Item().Text($"Username: {person.Username}");
                            x.Item().Text($"Temporary Password: {person.TemporaryPassword}");
                            x.Item().Text("Please change your password upon first login.");
                        });
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            })
            .GeneratePdf(pdfFilePath);

            return pdfFilePath;
        }
    }
}