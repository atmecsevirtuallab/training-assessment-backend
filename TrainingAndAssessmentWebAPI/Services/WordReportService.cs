using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class WordReportService(IWebHostEnvironment environment)
{
    private string TemplatePath => Path.Combine(environment.ContentRootPath, "App_Data", "Letterheads", "department-letterhead.docx");

    public bool HasTemplate => File.Exists(TemplatePath);

    public async Task SaveTemplateAsync(IFormFile file)
    {
        if (!string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The letterhead must be a Word .docx document.");
        if (file.Length == 0 || file.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Select a non-empty Word document smaller than 10 MB.");
        Directory.CreateDirectory(Path.GetDirectoryName(TemplatePath)!);
        var temporary = TemplatePath + ".upload";
        await using (var output = File.Create(temporary)) await file.CopyToAsync(output);
        try
        {
            using (var document = WordprocessingDocument.Open(temporary, false))
            {
                if (document.MainDocumentPart?.Document?.Body is null)
                    throw new InvalidOperationException("The Word document is invalid.");
            }
            File.Move(temporary, TemplatePath, true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    public byte[] Generate(SessionWordReport request)
    {
        if (!HasTemplate) throw new InvalidOperationException("Upload the department letterhead before generating a report.");
        using var stream = new MemoryStream();
        var templateBytes = File.ReadAllBytes(TemplatePath);
        stream.Write(templateBytes, 0, templateBytes.Length);
        stream.Position = 0;
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var body = document.MainDocumentPart!.Document.Body!;
            var sectionProperties = body.Elements<SectionProperties>().LastOrDefault();
            void Add(OpenXmlElement element) { if (sectionProperties is null) body.Append(element); else body.InsertBefore(element, sectionProperties); }
            Add(ParagraphText(request.TrainingName, true, 32, JustificationValues.Center));
            Add(ParagraphText($"Session Report - {request.SessionName}", true, 26, JustificationValues.Center));
            Add(FourColumnTable(new[] {
                ("Date", request.Date, "Time", request.Time),
                ("Batch", request.Batch, "Batch Strength/Count", request.BatchStrength.ToString()),
                ("Trainer", request.Trainer, "Classroom", request.Classroom)
            }));
            Add(Heading("Attendance"));
            Add(DetailsTable(new[] { ("No. of students in the batch", request.BatchStrength.ToString()), ("No. of students present for the training", request.Present.ToString()), ("Present percentage", $"{request.PresentPercentage:0.0}%"), ("No. of students absent for the training", request.Absent.ToString()), ("Absent percentage", $"{request.AbsentPercentage:0.0}%") }));
            Add(Heading("Photos of the Session"));
            Add(PhotoTable(document.MainDocumentPart, request.Photos));
            foreach (var section in request.Sections)
            {
                Add(Heading(section.Title));
                Add(DetailsTable(section.Items.Select(item => (item.Label, item.Value)).ToArray()));
            }
            Add(Heading("Overall Performance Analysis of the Batch"));
            Add(ParagraphText(request.OverallAnalysis, false, 22, JustificationValues.Left));
            document.MainDocumentPart.Document.Save();
        }
        return stream.ToArray();
    }

    private static Paragraph Heading(string text) => ParagraphText(text, true, 25, JustificationValues.Left);
    private static Paragraph ParagraphText(string text, bool bold, int size, JustificationValues alignment) =>
        new(new ParagraphProperties(new Justification { Val = alignment }, new SpacingBetweenLines { Before = "140", After = "100" }), new Run(new RunProperties(new Bold { Val = bold }, new FontSize { Val = size.ToString() }, new Color { Val = "000000" }), new Text(text ?? string.Empty)));
    private static Table DetailsTable((string Label, string Value)[] values)
    {
        var table = new Table(new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }, new TableBorders(
            new TopBorder { Val = BorderValues.Single, Color = "D9D9D9" }, new BottomBorder { Val = BorderValues.Single, Color = "D9D9D9" },
            new LeftBorder { Val = BorderValues.Single, Color = "D9D9D9" }, new RightBorder { Val = BorderValues.Single, Color = "D9D9D9" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Color = "D9D9D9" }, new InsideVerticalBorder { Val = BorderValues.Single, Color = "D9D9D9" })));
        foreach (var item in values) table.Append(new TableRow(Cell(item.Label, true), Cell(item.Value, false)));
        return table;
    }
    private static Table FourColumnTable((string Label1, string Value1, string Label2, string Value2)[] values)
    {
        var table = BaseTable();
        table.GetFirstChild<TableProperties>()!.Append(new TableLayout { Type = TableLayoutValues.Fixed });
        table.Append(new TableGrid(new GridColumn { Width = "1700" }, new GridColumn { Width = "3000" }, new GridColumn { Width = "2100" }, new GridColumn { Width = "3200" }));
        foreach (var item in values) table.Append(new TableRow(Cell(item.Label1, true), Cell(item.Value1, false), Cell(item.Label2, true), Cell(item.Value2, false)));
        return table;
    }
    private static Table BaseTable() => new(new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }, new TableBorders(
        new TopBorder { Val = BorderValues.Single, Color = "D9D9D9" }, new BottomBorder { Val = BorderValues.Single, Color = "D9D9D9" },
        new LeftBorder { Val = BorderValues.Single, Color = "D9D9D9" }, new RightBorder { Val = BorderValues.Single, Color = "D9D9D9" },
        new InsideHorizontalBorder { Val = BorderValues.Single, Color = "D9D9D9" }, new InsideVerticalBorder { Val = BorderValues.Single, Color = "D9D9D9" })));

    private static Table PhotoTable(MainDocumentPart mainPart, List<string> photos)
    {
        var table = BaseTable();
        var row = new TableRow();
        for (var index = 0; index < 2; index++)
        {
            var cell = new TableCell(new TableCellProperties(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct }));
            if (index < photos.Count && TryDecodeImage(photos[index], out var bytes, out var type))
                cell.Append(ImageParagraph(mainPart, bytes, type, (uint)(index + 1)));
            else
                cell.Append(ParagraphText($"Session Photo {index + 1} not uploaded", false, 20, JustificationValues.Center));
            row.Append(cell);
        }
        table.Append(row);
        return table;
    }

    private static bool TryDecodeImage(string value, out byte[] bytes, out PartTypeInfo type)
    {
        bytes = []; type = ImagePartType.Jpeg;
        try
        {
            var comma = value.IndexOf(','); var metadata = comma >= 0 ? value[..comma] : string.Empty;
            bytes = Convert.FromBase64String(comma >= 0 ? value[(comma + 1)..] : value);
            type = metadata.Contains("png", StringComparison.OrdinalIgnoreCase) ? ImagePartType.Png : ImagePartType.Jpeg;
            return bytes.Length > 0;
        }
        catch { return false; }
    }

    private static Paragraph ImageParagraph(MainDocumentPart mainPart, byte[] bytes, PartTypeInfo type, uint id)
    {
        var imagePart = mainPart.AddImagePart(type);
        using (var imageStream = new MemoryStream(bytes)) imagePart.FeedData(imageStream);
        var relationshipId = mainPart.GetIdOfPart(imagePart);
        const long width = 2743200; const long height = 1828800;
        var drawing = new Drawing(new DW.Inline(new DW.Extent { Cx = width, Cy = height }, new DW.EffectExtent(),
            new DW.DocProperties { Id = id, Name = $"Session photo {id}" }, new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(new PIC.Picture(new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = id, Name = $"Session photo {id}" }, new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = width, Cy = height }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
        return new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }), new Run(drawing));
    }
    private static TableCell Cell(string value, bool label) => new(new TableCellProperties(new TableCellMargin(new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa }, new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa }, new TableCellLeftMargin { Width = 120, Type = TableWidthValues.Dxa }, new TableCellRightMargin { Width = 120, Type = TableWidthValues.Dxa }), label ? new Shading { Fill = "EAF2F8" } : new Shading { Fill = "FFFFFF" }), ParagraphText(value, label, 21, JustificationValues.Left));
}

public sealed record SessionWordReport(string TrainingName, string SessionName, string Date, string Time, string Batch, int BatchStrength, string Trainer, string Classroom, int Present, int Absent, double PresentPercentage, double AbsentPercentage, List<string> Photos, List<WordReportSection> Sections, string OverallAnalysis);
public sealed record WordReportSection(string Title, List<WordReportItem> Items);
public sealed record WordReportItem(string Label, string Value);
