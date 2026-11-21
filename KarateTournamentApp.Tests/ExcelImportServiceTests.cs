using System.IO;
using KarateClassLibrary;
using KarateTournamentApp.Services;
using OfficeOpenXml;
using Xunit;

namespace KarateTournamentApp.Tests;

public sealed class ExcelImportServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"karate-excel-test-{Guid.NewGuid():N}.xlsx");
    private readonly ExcelImportService _service = new();

    [Fact]
    public async Task GeneratedTemplateImportsFilledRowsAndCanReplaceAnExistingWorkbook()
    {
        await _service.CreateSampleExcelFileAsync(_path);
        Assert.Empty(await _service.ImportParticipantsFromExcelAsync(_path, rejectInvalidRows: true));

        using (var workbook = new ExcelPackage(new FileInfo(_path)))
        {
            var sheet = workbook.Workbook.Worksheets["Participants"];
            Assert.Equal("FirstName", sheet.Cells["A1"].Text);
            Assert.Equal("Category3", sheet.Cells["I1"].Text);
            Assert.NotNull(workbook.Workbook.Worksheets["Instructions"]);
            sheet.Cells["A2"].Value = "Łucja";
            sheet.Cells["B2"].Value = "Żółć";
            sheet.Cells["C2"].Value = 12;
            sheet.Cells["D2"].Value = "Łódź";
            sheet.Cells["E2"].Value = "Kyu8";
            sheet.Cells["F2"].Value = "Female";
            sheet.Cells["G2"].Value = "Kata";
            sheet.Cells["I2"].Value = "Kihon";
            workbook.Save();
        }

        var person = Assert.Single(await _service.ImportParticipantsFromExcelAsync(_path, rejectInvalidRows: true));
        Assert.Equal("Łucja", person.FirstName);
        Assert.Equal("Żółć", person.LastName);
        Assert.Equal(12, person.Age);
        Assert.Equal("Łódź", person.Club);
        Assert.Equal(Belts.Kyu8, person.Belt);
        Assert.Equal(Sex.Female, person.Sex);
        Assert.Equal(new[] { CategoryType.Kata, CategoryType.Kihon }, person.Categories);

        await _service.CreateSampleExcelFileAsync(_path);
        Assert.Empty(await _service.ImportParticipantsFromExcelAsync(_path, rejectInvalidRows: true));
    }

    public void Dispose() => File.Delete(_path);
}
