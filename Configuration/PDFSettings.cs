namespace BatchBaker.Configuration
{
    public class PDFSettings
    {
        // Folder the PDFs are saved under, one subfolder per run. Leave blank to use
        // a "PDF" folder on the user's real Desktop, which also works when OneDrive
        // has moved the Desktop. Environment variables like %USERPROFILE% are expanded.
        public string FilePath { get; set; } = string.Empty;
    }
}
