using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Services;

public sealed class PdfExportService
{
    public Task ExportNotebookAsync(Notebook notebook, string targetPath, CancellationToken cancellationToken = default)
    {
        // Fase 6: se renderizaran las paginas WPF y se compondran con QuestPDF.
        throw new NotImplementedException("La exportacion PDF esta preparada para la Fase 6.");
    }
}
