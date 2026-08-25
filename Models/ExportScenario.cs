namespace OutOfMemoryWorkbook.Models;

public sealed record ExportScenario(
    string Route,
    string DataSource,
    string Workbook,
    string Target,
    string Objective);
