using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.SqlClient;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class EventTeamWorkbookService(IConfiguration configuration)
{
    private readonly string connectionString = configuration.GetConnectionString("TrainingAssessmentPortal")
        ?? throw new InvalidOperationException("Database connection is missing.");

    public async Task<byte[]> GenerateAsync()
    {
        var records = new List<TeamDetailRow>();
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            const string sql = """
                SELECT COALESCE(t.TeamNumber,t.RegistrationReference,''),m.Name,m.USN,m.Mobile,m.Email,m.Branch,
                       COALESCE(t.Mentor,''),e.EventName,COALESCE(e.EventType,''),e.HostingInstitution,COALESCE(e.Venue,''),
                       e.EventFrom,e.EventTo,
                       CASE WHEN COALESCE(t.PostEventStatus,'Yet to update') IN ('Yet to update','Registered') THEN 'Registered'
                            ELSE t.PostEventStatus END,
                       COALESCE(t.CoordinatorRemarks,'')
                FROM EventTeams t
                JOIN ExternalEvents e ON e.EventId=t.EventId
                JOIN EventTeamMembers m ON m.TeamId=t.TeamId
                WHERE t.IsDeleted=0
                ORDER BY t.CreatedAt DESC,t.TeamId,m.IsLeader DESC,m.MemberId
                """;
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                records.Add(new(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                    reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetDateTime(11), reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    reader.GetString(13), reader.GetString(14)));
        }

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStyles();
            stylesPart.Stylesheet.Save();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var mergeCells = new MergeCells(
                new MergeCell { Reference = "A1:A2" }, new MergeCell { Reference = "B1:F1" }, new MergeCell { Reference = "G1:G2" },
                new MergeCell { Reference = "H1:H2" }, new MergeCell { Reference = "I1:I2" }, new MergeCell { Reference = "J1:J2" },
                new MergeCell { Reference = "K1:K2" }, new MergeCell { Reference = "L1:L2" }, new MergeCell { Reference = "M1:N1" },
                new MergeCell { Reference = "O1:O2" }, new MergeCell { Reference = "P1:P2" });
            worksheetPart.Worksheet = new Worksheet(
                new SheetViews(new SheetView(new Pane { VerticalSplit = 2D, TopLeftCell = "A3", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen }) { WorkbookViewId = 0U }),
                CreateColumns(), sheetData, mergeCells);

            sheetData.Append(new Row(
                Text("A1", "Team#", 1), Text("B1", "Team Members", 1), Text("G1", "Mentor", 1), Text("H1", "Event Name", 1),
                Text("I1", "Event Type", 1), Text("J1", "Hosting Organization", 1), Text("K1", "Location", 1), Text("L1", "No. of Days", 1),
                Text("M1", "Event Dates", 1), Text("O1", "Status (Participated/Not Participated/Won the Competition)", 1), Text("P1", "Remarks", 1)) { RowIndex = 1 });
            sheetData.Append(new Row(
                Text("B2", "Name", 2), Text("C2", "USN", 2), Text("D2", "Contact", 2), Text("E2", "Email Id", 2), Text("F2", "Branch", 2),
                Text("M2", "From Date", 2), Text("N2", "To Date", 2)) { RowIndex = 2 });

            uint rowNumber = 3;
            foreach (var team in records.GroupBy(item => item.TeamNumber))
            {
                var firstRow = rowNumber;
                var firstMember = true;
                foreach (var item in team)
                {
                    var days = item.FromDate.HasValue && item.ToDate.HasValue ? (item.ToDate.Value.Date - item.FromDate.Value.Date).Days + 1 : 0;
                    sheetData.Append(new Row(
                        Text($"A{rowNumber}", firstMember ? item.TeamNumber : "", 5), Text($"B{rowNumber}", item.Name, 3), Text($"C{rowNumber}", item.Usn, 3),
                        Text($"D{rowNumber}", item.Contact, 3), Text($"E{rowNumber}", item.Email, 3), Text($"F{rowNumber}", item.Branch, 3),
                        Text($"G{rowNumber}", firstMember ? item.Mentor : "", 5), Text($"H{rowNumber}", firstMember ? item.EventName : "", 5), Text($"I{rowNumber}", firstMember ? item.EventType : "", 5),
                        Text($"J{rowNumber}", firstMember ? item.HostingOrganization : "", 5), Text($"K{rowNumber}", firstMember ? item.Location : "", 5), firstMember ? Number($"L{rowNumber}", days, 5) : Text($"L{rowNumber}", "", 5),
                        Date($"M{rowNumber}", firstMember ? item.FromDate : null, 6), Date($"N{rowNumber}", firstMember ? item.ToDate : null, 6), Text($"O{rowNumber}", firstMember ? item.Status : "", 5),
                        Text($"P{rowNumber}", item.Remarks, 3)) { RowIndex = rowNumber });
                    firstMember = false;
                    rowNumber++;
                }
                var lastRow = rowNumber - 1;
                if (lastRow > firstRow)
                    foreach (var column in new[] { "A", "G", "H", "I", "J", "K", "L", "M", "N", "O" })
                        mergeCells.Append(new MergeCell { Reference = $"{column}{firstRow}:{column}{lastRow}" });
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1U, Name = "CSE" });
            workbookPart.Workbook.Save();
        }
        return stream.ToArray();
    }

    private static Columns CreateColumns() => new(
        Width(1, 18), Width(2, 22), Width(3, 16), Width(4, 16), Width(5, 30), Width(6, 14), Width(7, 24), Width(8, 30),
        Width(9, 26), Width(10, 30), Width(11, 30), Width(12, 12), Width(13, 14), Width(14, 14), Width(15, 28), Width(16, 28));
    private static Column Width(uint index, double width) => new() { Min = index, Max = index, Width = width, CustomWidth = true };
    private static Cell Text(string reference, string? value, uint style) => new() { CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style, InlineString = new InlineString(new Text(value ?? "")) };
    private static Cell Number(string reference, int value, uint style) => new() { CellReference = reference, DataType = CellValues.Number, StyleIndex = style, CellValue = new CellValue(value) };
    private static Cell Date(string reference, DateTime? value, uint style = 4U) => value.HasValue
        ? new Cell { CellReference = reference, DataType = CellValues.Number, StyleIndex = style, CellValue = new CellValue(value.Value.ToOADate()) }
        : Text(reference, "", style);

    private static Stylesheet CreateStyles()
    {
        var fonts = new Fonts(
            new Font(new FontSize { Val = 11D }, new FontName { Val = "Aptos" }),
            new Font(new Bold(), new Color { Rgb = "FFFFFFFF" }, new FontSize { Val = 11D }, new FontName { Val = "Aptos" }));
        var fills = new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FF172554" }) { PatternType = PatternValues.Solid }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFDCE6F1" }) { PatternType = PatternValues.Solid }));
        var borders = new Borders(new Border(), new Border(
            new LeftBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FFB8C5D6" } },
            new RightBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FFB8C5D6" } },
            new TopBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FFB8C5D6" } },
            new BottomBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FFB8C5D6" } }, new DiagonalBorder()));
        var formats = new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 1U, FillId = 2U, BorderId = 1U, ApplyAlignment = true, Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center, WrapText = true } },
            new CellFormat { FontId = 1U, FillId = 3U, BorderId = 1U, ApplyAlignment = true, Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center, WrapText = true } },
            new CellFormat { FontId = 0U, FillId = 0U, BorderId = 1U, ApplyAlignment = true, Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center, WrapText = true } },
            new CellFormat { FontId = 0U, FillId = 0U, BorderId = 1U, NumberFormatId = 14U, ApplyNumberFormat = true, ApplyAlignment = true, Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center } },
            new CellFormat { FontId = 0U, FillId = 0U, BorderId = 1U, ApplyAlignment = true, Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Left, Vertical = VerticalAlignmentValues.Center, WrapText = true } },
            new CellFormat { FontId = 0U, FillId = 0U, BorderId = 1U, NumberFormatId = 14U, ApplyNumberFormat = true, ApplyAlignment = true, Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Left, Vertical = VerticalAlignmentValues.Center } });
        return new Stylesheet(fonts, fills, borders, new CellStyleFormats(new CellFormat()), formats);
    }

    private sealed record TeamDetailRow(string TeamNumber, string Name, string Usn, string Contact, string Email, string Branch,
        string Mentor, string EventName, string EventType, string HostingOrganization, string Location, DateTime? FromDate,
        DateTime? ToDate, string Status, string Remarks);
}
