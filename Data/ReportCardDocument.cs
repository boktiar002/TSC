using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TSC.Models;

namespace TSC.Data;

// The report card as a file a guardian can keep, forward and show at an admission interview
// years later. The printed sheet and this are deliberately the same document in two media; if
// one changes, change the other (Areas/Admin/Views/Exams/ReportCard.cshtml).
public static class ReportCardDocument
{
    // Ink, matched to the web sheet.
    private const string Navy = "#102133";
    private const string Brand = "#1d4ed8";
    private const string Muted = "#64748b";
    private const string Line = "#d9e1ee";
    private const string Shade = "#f5f8fc";
    private const string Gold = "#b4883b";

    private static byte[]? _logo;

    // Pre-faded on disk rather than faded here: this QuestPDF has no opacity on an image, and
    // a one-off asset beats pulling in an imaging library to do at runtime what never changes.
    // Replace the master artwork and run tools/make-logo-assets.ps1 to rebuild this.
    private static byte[]? _watermark;

    private static bool _ready;

    // Bengali is the script the students' names are actually written in. The glyphs come from a
    // font bundled with the app rather than from the host: a Linux server has no Bengali face
    // installed by default and every name would render as empty boxes.
    private static void Initialise(string webRoot)
    {
        if (_ready)
            return;

        QuestPDF.Settings.License = LicenseType.Community;

        var font = Path.Combine(webRoot, "fonts", "NotoSansBengali.ttf");

        if (File.Exists(font))
            QuestPDF.Drawing.FontManager.RegisterFontFromStream(File.OpenRead(font));

        var logo = Path.Combine(webRoot, "img", "tsc-logo.png");

        if (File.Exists(logo))
            _logo = File.ReadAllBytes(logo);

        var watermark = Path.Combine(webRoot, "img", "tsc-watermark.png");

        if (File.Exists(watermark))
            _watermark = File.ReadAllBytes(watermark);

        _ready = true;
    }

    public static byte[] Render(ReportCardModel card, string webRoot) =>
        Build(card, webRoot).GeneratePdf();

    private static IDocument Build(ReportCardModel card, string webRoot)
    {
        Initialise(webRoot);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontFamily("Noto Sans Bengali").FontSize(9).FontColor(Navy));

                // The whole sheet sits inside a ruled frame, the way the centre's existing
                // paper certificates do.
                page.Content().Border(1.2f).BorderColor(Gold).Padding(3).Element(outer =>
                    outer.Border(0.5f).BorderColor(Line).Padding(16).Layers(layers =>
                    {
                        // Layer one is the watermark: behind everything, and large enough to be
                        // obvious on a photocopy without fighting the text for legibility.
                        layers.Layer().AlignCenter().AlignMiddle().Element(e => Watermark(e));

                        layers.PrimaryLayer().Column(col =>
                        {
                            col.Item().Element(e => Header(e, card));
                            col.Item().PaddingTop(10).Element(e => StudentPanel(e, card));
                            col.Item().PaddingTop(10).Element(e => MarksTable(e, card));
                            col.Item().PaddingTop(10).Element(e => Summary(e, card));

                            // Deliberately a fixed gap rather than ExtendVertical: extending
                            // fills the remaining space and then pushes the signatures onto a
                            // second page, and a report card has to be one sheet.
                            col.Item().PaddingTop(34).Element(e => Signatures(e));
                        });
                    }));

                page.Footer().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("TSC — Teacher Student Care")
                        .FontSize(7).FontColor(Muted);

                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7).FontColor(Muted));
                        t.Span("Computer-generated report · issued ");
                        t.Span(Clock.Today.ToString("dd MMM yyyy"));
                    });
                });
            });
        });
    }

    private static void Watermark(IContainer e)
    {
        if (_watermark == null)
            return;

        e.Width(340).Image(_watermark).FitWidth();
    }

    private static void Header(IContainer e, ReportCardModel card)
    {
        e.Column(col =>
        {
            col.Item().Row(row =>
            {
                if (_logo != null)
                    row.ConstantItem(62).AlignMiddle().Image(_logo).FitWidth();

                row.RelativeItem().PaddingLeft(10).Column(c =>
                {
                    c.Item().Text("TSC — Teacher Student Care")
                        .FontSize(16).Bold().FontColor(Navy);

                    c.Item().Text("Coaching centre · Bangladesh")
                        .FontSize(7.5f).FontColor(Muted).LetterSpacing(0.14f);
                });

                row.ConstantItem(130).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("PROGRESS REPORT")
                        .FontSize(8).Bold().FontColor(Gold).LetterSpacing(0.18f);

                    c.Item().AlignRight().Text(card.Title)
                        .FontSize(9).SemiBold().FontColor(Navy);

                    c.Item().AlignRight().Text($"Issued {Clock.Today:dd MMM yyyy}")
                        .FontSize(7.5f).FontColor(Muted);
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Gold);
        });
    }

    private static void StudentPanel(IContainer e, ReportCardModel card)
    {
        var s = card.Student;

        e.Background(Shade).Border(0.5f).BorderColor(Line).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                Field(c, "Student", s.FullName, strong: true);
                Field(c, "Student ID", s.StudentId);
                Field(c, "Class & batch",
                    s.SchoolClass?.Name + (string.IsNullOrWhiteSpace(s.Batch) ? "" : " · " + s.Batch));
            });

            row.ConstantItem(14);

            row.RelativeItem().Column(c =>
            {
                Field(c, "Guardian", string.IsNullOrWhiteSpace(s.GuardianName) ? "—" : s.GuardianName);
                Field(c, "Contact", s.GuardianPhone ?? s.Phone ?? "—");
                Field(c, "Exams on this card", string.Join(", ", card.Chosen.Select(x => x.Name)));
            });
        });
    }

    private static void Field(ColumnDescriptor c, string label, string? value, bool strong = false)
    {
        c.Item().PaddingBottom(3).Row(row =>
        {
            row.ConstantItem(78).Text(label).FontSize(7.5f).FontColor(Muted);

            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(strong ? 10 : 9)
                .SemiBold();
        });
    }

    private static void MarksTable(IContainer e, ReportCardModel card)
    {
        if (card.Columns.Count == 0)
        {
            e.Text("No papers have been marked for this class yet.").FontColor(Muted).Italic();
            return;
        }

        e.Column(outer =>
        {
            outer.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.6f);  // exam
                    c.RelativeColumn(2.4f);  // paper
                    c.RelativeColumn(1.1f);  // full marks
                    c.RelativeColumn(1.1f);  // marks
                    c.RelativeColumn(1.0f);  // percent
                    c.RelativeColumn(0.9f);  // grade
                });

                // With one exam on the card its name is already in the masthead; repeating it on
                // every row is noise. With several it has to say which row belongs to which.
                var manyExams = card.Chosen.Count > 1;

                table.Header(h =>
                {
                    HeadCell(h, manyExams ? "Exam" : "Date", left: true);
                    HeadCell(h, "Paper", left: true);
                    HeadCell(h, "Full marks");
                    HeadCell(h, "Marks");
                    HeadCell(h, "%");
                    HeadCell(h, "Grade");
                });

                var zebra = false;

                foreach (var line in card.Lines())
                {
                    // Odd rows carry no fill at all, so the watermark shows through the table
                    // rather than being blanked out by a sheet of white.
                    var bg = zebra ? "#f7fafd" : null;
                    zebra = !zebra;

                    Cell(table, bg).AlignMiddle().Column(c =>
                    {
                        if (manyExams)
                        {
                            c.Item().Text(line.ExamName).SemiBold();
                            c.Item().Text(line.ExamDate.ToString("dd MMM yyyy")).FontSize(7).FontColor(Muted);
                        }
                        else
                        {
                            c.Item().Text(line.ExamDate.ToString("dd MMM yyyy"));
                        }
                    });

                    Cell(table, bg).AlignMiddle().Text(line.SubjectName);
                    Cell(table, bg).AlignMiddle().AlignRight().Text(line.FullMarks.ToString("0.##"));

                    Cell(table, bg).AlignMiddle().AlignRight().Text(t =>
                    {
                        if (line.Score == null)
                            t.Span("Absent").FontColor("#b91c1c").SemiBold();
                        else
                            t.Span(line.Score.Value.ToString("0.##")).SemiBold();
                    });

                    Cell(table, bg).AlignMiddle().AlignRight().Text($"{line.Percent:0.#}%");

                    Cell(table, bg).AlignMiddle().AlignCenter()
                        .Text(line.GradeLetter).SemiBold().FontColor(Brand);
                }
            });

            // Totals sit outside the table so the rule above them can be heavier than the
            // row lines without the header picking it up too.
            outer.Item().BorderTop(1).BorderColor(Navy).Background("#eef3fb").Padding(6).Row(row =>
            {
                row.RelativeItem(5f).Text("TOTAL").Bold().FontSize(9).LetterSpacing(0.1f);
                row.RelativeItem(1.1f).AlignRight().Text(card.FullMarks.ToString("0.##")).Bold();
                row.RelativeItem(1.1f).AlignRight().Text(card.Row.Total.ToString("0.##")).Bold();
                row.RelativeItem(1.0f).AlignRight().Text($"{card.Row.AveragePercent:0.#}%").Bold();
                row.RelativeItem(0.9f).AlignCenter().Text(card.Grade.Letter).Bold().FontColor(Brand);
            });
        });
    }

    private static void HeadCell(TableCellDescriptor h, string text, bool left = false)
    {
        var cell = h.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(5);

        (left ? cell : cell.AlignRight())
            .Text(text).FontSize(7.5f).Bold().FontColor("#ffffff").LetterSpacing(0.08f);
    }

    // null background = leave the page showing through, which is what lets the watermark read
    // under the table instead of being covered by a sheet of white cells.
    private static IContainer Cell(TableDescriptor table, string? background)
    {
        var cell = table.Cell();

        return (background == null ? cell : cell.Background(background))
            .BorderBottom(0.5f).BorderColor(Line)
            .PaddingVertical(4).PaddingHorizontal(5);
    }

    private static void Summary(IContainer e, ReportCardModel card)
    {
        e.Row(row =>
        {
            Stat(row, "Merit position", card.Place,
                card.Row.Rank == 0 ? "not in the class ranking" : $"of {card.ClassSize} in the class");

            row.ConstantItem(8);

            Stat(row, "Papers sat", $"{card.Row.PapersSat} / {card.Columns.Count}",
                "absent counts as zero");

            row.ConstantItem(8);

            Stat(row, "Attendance",
                card.AttendanceRate == null ? "—" : $"{card.AttendanceRate}%",
                $"{card.AttendancePresent} of {card.AttendanceDays} days");

            row.ConstantItem(8);

            Stat(row, "GPA", card.Grade.Point.ToString("0.00"), $"grade {card.Grade.Letter}");
        });
    }

    private static void Stat(RowDescriptor row, string label, string value, string note)
    {
        row.RelativeItem().Border(0.5f).BorderColor(Line).Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(7).FontColor(Muted).LetterSpacing(0.08f);
            c.Item().PaddingTop(1).Text(value).FontSize(14).Bold().FontColor(Navy);
            c.Item().Text(note).FontSize(6.5f).FontColor(Muted);
        });
    }

    private static void Signatures(IContainer e)
    {
        e.Row(row =>
        {
            row.RelativeItem(2).Column(c =>
            {
                c.Item().Text("Teacher's remark").FontSize(7.5f).FontColor(Muted);
                c.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor(Line);
                c.Item().PaddingTop(14).LineHorizontal(0.5f).LineColor(Line);
            });

            row.ConstantItem(24);

            row.RelativeItem().AlignBottom().Column(c =>
            {
                c.Item().PaddingTop(30).LineHorizontal(0.5f).LineColor(Navy);
                c.Item().PaddingTop(3).AlignCenter().Text("Guardian's signature")
                    .FontSize(7.5f).FontColor(Muted);
            });

            row.ConstantItem(24);

            row.RelativeItem().AlignBottom().Column(c =>
            {
                c.Item().PaddingTop(30).LineHorizontal(0.5f).LineColor(Navy);
                c.Item().PaddingTop(3).AlignCenter().Text("Principal")
                    .FontSize(7.5f).FontColor(Muted);
            });
        });
    }
}
