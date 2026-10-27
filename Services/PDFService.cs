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
        private readonly string _baseFolder;

        public PDFService(IOptions<PDFSettings> pdfSettings)
        {
            string configured = pdfSettings.Value.FilePath;
            _baseFolder = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PDF")
                : Environment.ExpandEnvironmentVariables(configured);
        }

        // A new timestamped subfolder for one run, so each batch's PDFs stay together
        // and a reused username can't overwrite an older PDF.
        public string CreateRunFolder()
        {
            string folder = Path.Combine(_baseFolder, DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        public string GeneratePDF(People person, string folder)
        {
            // Usernames come from the CSV, so strip anything Windows won't allow in a filename.
            string safeName = string.Concat(person.Username.Split(Path.GetInvalidFileNameChars()));
            string pdfFilePath = Path.Combine(folder, $"{safeName}_credentials.pdf");

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
                            x.Item().Text($"Kære {person.FirstName} din nye konto er nu oprettet. Her er dine loginoplysninger:");
                            x.Spacing(20);
                            x.Item().Text($"Username: {person.Username}");
                            x.Item().Text($"Temporary Password: {person.TemporaryPassword}");
                            x.Item().Text("Hust at skifte dit password ved første login.");
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