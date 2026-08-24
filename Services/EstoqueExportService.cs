using NPOI.SS.UserModel;
using NPOI.XSSF.Streaming;
using NPOI.XSSF.UserModel;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class EstoqueExportService(IEstoqueDataSource dataSource) : IEstoqueExportService
{
    private const int RowAccessWindowSize = 200;
    private const bool CompressTemporaryFiles = true;
    private const bool UseSharedStringsTable = false;

    public byte[] ExportarCenarioAtual(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var estoques = dataSource.CriarLista(quantidade, cancellationToken);
        var workbook = new XSSFWorkbook();

        try
        {
            PreencherWorkbook(workbook, estoques, cancellationToken);

            using var memoryStream = new MemoryStream();
            workbook.Write(memoryStream, leaveOpen: true);

            // Reproduz intencionalmente o cenário atual: o ToArray cria uma
            // segunda cópia do arquivo na memória.
            return memoryStream.ToArray();
        }
        finally
        {
            workbook.Close();
        }
    }

    public Stream ExportarXssfSemToArray(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var estoques = dataSource.CriarLista(quantidade, cancellationToken);
        var workbook = new XSSFWorkbook();
        var memoryStream = new MemoryStream();

        try
        {
            PreencherWorkbook(workbook, estoques, cancellationToken);
            workbook.Write(memoryStream, leaveOpen: true);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch
        {
            memoryStream.Dispose();
            throw;
        }
        finally
        {
            workbook.Close();
        }
    }

    public byte[] ExportarSxssfComLista(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var estoques = dataSource.CriarLista(quantidade, cancellationToken);
        var workbook = CriarSxssfWorkbook();

        try
        {
            PreencherWorkbook(workbook, estoques, cancellationToken);

            using var memoryStream = new MemoryStream();
            workbook.Write(memoryStream, leaveOpen: true);
            return memoryStream.ToArray();
        }
        finally
        {
            workbook.Dispose();
        }
    }

    public string ExportarSxssfParaArquivoTemporario(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var caminho = Path.Combine(
            Path.GetTempPath(),
            $"estoque-{Guid.NewGuid():N}.xlsx");

        try
        {
            using var arquivo = new FileStream(
                caminho,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);

            ExportarSxssfParaStream(quantidade, arquivo, cancellationToken);
            return caminho;
        }
        catch
        {
            File.Delete(caminho);
            throw;
        }
    }

    public void ExportarSxssfParaStream(
        int quantidade,
        Stream destino,
        CancellationToken cancellationToken)
    {
        var workbook = CriarSxssfWorkbook();

        try
        {
            var estoques = dataSource.Stream(quantidade, cancellationToken);
            PreencherWorkbook(workbook, estoques, cancellationToken);
            workbook.Write(destino, leaveOpen: true);
        }
        finally
        {
            workbook.Dispose();
        }
    }

    private static SXSSFWorkbook CriarSxssfWorkbook()
    {
        return new SXSSFWorkbook(
            workbook: null!,
            rowAccessWindowSize: RowAccessWindowSize,
            compressTmpFiles: CompressTemporaryFiles,
            useSharedStringsTable: UseSharedStringsTable);
    }

    private static void PreencherWorkbook(
        IWorkbook workbook,
        IEnumerable<Estoque> estoques,
        CancellationToken cancellationToken)
    {
        var sheet = workbook.CreateSheet("Estoque");
        ConfigurarColunas(sheet);

        var styles = CriarEstilos(workbook);
        CriarCabecalho(sheet, styles.Cabecalho);

        var rowIndex = 1;

        foreach (var estoque in estoques)
        {
            if ((rowIndex & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var row = sheet.CreateRow(rowIndex++);

            row.CreateCell(0).SetCellValue((double)estoque.Id);
            row.CreateCell(1).SetCellValue(estoque.Codigo);
            row.CreateCell(2).SetCellValue(estoque.Descricao);
            row.CreateCell(3).SetCellValue(estoque.Categoria);
            row.CreateCell(4).SetCellValue(estoque.Deposito);
            row.CreateCell(5).SetCellValue(estoque.Quantidade);

            var custoCell = row.CreateCell(6);
            custoCell.SetCellValue((double)estoque.CustoUnitario);
            custoCell.CellStyle = styles.Monetario;

            var dataCell = row.CreateCell(7);
            dataCell.SetCellValue(estoque.UltimaMovimentacao);
            dataCell.CellStyle = styles.DataHora;
        }
    }

    private static void CriarCabecalho(ISheet sheet, ICellStyle headerStyle)
    {
        string[] titulos =
        [
            "Id",
            "Código",
            "Descrição",
            "Categoria",
            "Depósito",
            "Quantidade",
            "Custo unitário",
            "Última movimentação"
        ];

        var row = sheet.CreateRow(0);

        for (var index = 0; index < titulos.Length; index++)
        {
            var cell = row.CreateCell(index);
            cell.SetCellValue(titulos[index]);
            cell.CellStyle = headerStyle;
        }
    }

    private static EstilosWorkbook CriarEstilos(IWorkbook workbook)
    {
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerFont.Color = IndexedColors.White.Index;

        var headerStyle = workbook.CreateCellStyle();
        headerStyle.SetFont(headerFont);
        headerStyle.FillForegroundColor = IndexedColors.DarkBlue.Index;
        headerStyle.FillPattern = FillPattern.SolidForeground;

        var dataFormat = workbook.CreateDataFormat();

        var moneyStyle = workbook.CreateCellStyle();
        moneyStyle.DataFormat = dataFormat.GetFormat("R$ #,##0.00");

        var dateTimeStyle = workbook.CreateCellStyle();
        dateTimeStyle.DataFormat = dataFormat.GetFormat("dd/MM/yyyy HH:mm:ss");

        return new EstilosWorkbook(headerStyle, moneyStyle, dateTimeStyle);
    }

    private static void ConfigurarColunas(ISheet sheet)
    {
        sheet.SetColumnWidth(0, 14 * 256);
        sheet.SetColumnWidth(1, 18 * 256);
        sheet.SetColumnWidth(2, 38 * 256);
        sheet.SetColumnWidth(3, 18 * 256);
        sheet.SetColumnWidth(4, 18 * 256);
        sheet.SetColumnWidth(5, 14 * 256);
        sheet.SetColumnWidth(6, 18 * 256);
        sheet.SetColumnWidth(7, 24 * 256);
    }

    private sealed record EstilosWorkbook(
        ICellStyle Cabecalho,
        ICellStyle Monetario,
        ICellStyle DataHora);
}
