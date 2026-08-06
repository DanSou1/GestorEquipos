using GestorEquipos.Models.ViewModels.Desktop;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Gestor_Equipos.Services.Implementations
{
    public class DesktopPdfService : IDesktopPdfService
    {
        private static readonly Color HeadingBackground = new(248, 249, 250);
        private static readonly Color BorderColor = new(222, 226, 230);
        private static readonly Color MutedColor = new(108, 117, 125);
        private static readonly Color ActiveColor = new(25, 135, 84);

        static DesktopPdfService()
        {
            GlobalFontSettings.FontResolver ??= new LocalFontResolver();
        }

        public byte[] GenerateHojaDeVidaPdf(DesktopDetailViewModel detail)
        {
            var document = new Document();
            DefineStyles(document);

            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(2);
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);

            AddHeader(section, detail);
            AddSectionHeading(section, "Especificaciones");
            AddLabelValue(section, "Serial", detail.SerialNumber);
            AddLabelValue(section, "Marca / Modelo", $"{detail.Brand} / {detail.Model}");
            AddLabelValue(section, "Procesador", detail.Processor);
            AddLabelValue(section, "Disco", detail.Disk);
            AddLabelValue(section, "Sistema Operativo", detail.OSVersionName);
            AddLabelValue(section, "RAM", detail.RamSpecification);
            AddLabelValue(section, "Acceso remoto", detail.RemoteInfo ?? "No configurado");

            AddSectionHeading(section, "Asignación actual");
            AddLabelValue(section, "Usuario", detail.CurrentUserName);
            AddLabelValue(section, "Área", detail.CurrentAreaName);
            AddLabelValue(section, "Regional", detail.CurrentRegionalName);

            AddAssignmentHistory(section, detail);
            AddLicenses(section, detail);
            AddPeripherals(section, detail);
            AddMaintenances(section, detail);
            AddSpecChangeLogs(section, detail);

            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();

            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return stream.ToArray();
        }

        private static void DefineStyles(Document document)
        {
            var normal = document.Styles["Normal"]!;
            normal.Font.Name = "Verdana";
            normal.Font.Size = 9;

            var heading1 = document.Styles["Heading1"]!;
            heading1.Font.Size = 18;
            heading1.Font.Bold = true;
            heading1.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);

            var heading2 = document.Styles["Heading2"]!;
            heading2.Font.Size = 11;
            heading2.Font.Bold = true;
            heading2.ParagraphFormat.Shading.Color = HeadingBackground;
            heading2.ParagraphFormat.Borders.Bottom.Width = Unit.FromPoint(0.75);
            heading2.ParagraphFormat.Borders.Bottom.Color = BorderColor;
            heading2.ParagraphFormat.Borders.DistanceFromBottom = Unit.FromPoint(3);
            heading2.ParagraphFormat.SpaceBefore = Unit.FromPoint(14);
            heading2.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        }

        private static void AddHeader(Section section, DesktopDetailViewModel detail)
        {
            var title = section.AddParagraph(detail.NameDesktop);
            title.Style = "Heading1";

            var meta = section.AddParagraph();
            meta.Format.SpaceAfter = Unit.FromPoint(4);
            meta.AddFormattedText("Estado: ", TextFormat.Bold);
            var badge = meta.AddFormattedText(detail.Estado ? "Activo" : "Inactivo");
            badge.Bold = true;
            badge.Color = detail.Estado ? ActiveColor : MutedColor;
            meta.AddText("    ");
            meta.AddFormattedText("Generado: ", TextFormat.Bold);
            meta.AddText(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        }

        private static void AddSectionHeading(Section section, string title)
        {
            var paragraph = section.AddParagraph(title);
            paragraph.Style = "Heading2";
        }

        private static void AddLabelValue(Section section, string label, string value)
        {
            var paragraph = section.AddParagraph();
            paragraph.Format.SpaceAfter = Unit.FromPoint(2);
            paragraph.AddFormattedText($"{label}: ", TextFormat.Bold);
            paragraph.AddText(value);
        }

        private static void AddEmptyNote(Section section, string text)
        {
            var paragraph = section.AddParagraph(text);
            paragraph.Format.Font.Italic = true;
            paragraph.Format.Font.Color = MutedColor;
            paragraph.Format.SpaceAfter = Unit.FromPoint(4);
        }

        private static void AddAssignmentHistory(Section section, DesktopDetailViewModel detail)
        {
            AddSectionHeading(section, "Historial de asignaciones");
            if (detail.AsignationHistory.Count == 0)
            {
                AddEmptyNote(section, "Sin historial de asignaciones.");
                return;
            }

            var table = AddTable(section, new[] { "Usuario", "Fecha" }, new[] { 11.0, 4.0 });
            foreach (var a in detail.AsignationHistory)
            {
                AddRow(table, a.UserName, a.DateAsignation.ToString("yyyy-MM-dd"));
            }
        }

        private static void AddLicenses(Section section, DesktopDetailViewModel detail)
        {
            AddSectionHeading(section, "Licencias");
            if (detail.Licenses.Count == 0)
            {
                AddEmptyNote(section, "Sin licencias registradas.");
                return;
            }

            foreach (var l in detail.Licenses)
            {
                var value = l.NoLicense ? "Sin licencia" : l.LicenseKey ?? "-";
                AddLabelValue(section, l.SoftwareType, value);
            }
        }

        private static void AddPeripherals(Section section, DesktopDetailViewModel detail)
        {
            AddSectionHeading(section, "Periféricos");
            if (detail.Peripherals.Count == 0)
            {
                AddEmptyNote(section, "Sin periféricos registrados.");
                return;
            }

            foreach (var p in detail.Peripherals)
            {
                var line = section.AddParagraph();
                line.Format.SpaceBefore = Unit.FromPoint(4);
                line.AddFormattedText($"{p.TypeName} — {p.Brand} {p.Model}", TextFormat.Bold);
                if (!string.IsNullOrWhiteSpace(p.Serial))
                {
                    line.AddText($" (Serial: {p.Serial})");
                }

                line.AddText($"  [{p.Estado}]");

                foreach (var o in p.Observations)
                {
                    var obsParagraph = section.AddParagraph();
                    obsParagraph.Format.LeftIndent = Unit.FromCentimeter(0.5);
                    obsParagraph.Format.Font.Size = 8;
                    obsParagraph.Format.Font.Color = MutedColor;
                    obsParagraph.AddText($"{o.Date:yyyy-MM-dd} — {o.Type}: {o.Description}");
                }
            }
        }

        private static void AddMaintenances(Section section, DesktopDetailViewModel detail)
        {
            AddSectionHeading(section, "Historial de mantenimiento");
            if (detail.Maintenances.Count == 0)
            {
                AddEmptyNote(section, "Sin mantenimientos registrados.");
                return;
            }

            var table = AddTable(section, new[] { "Tipo", "Fecha", "Descripción", "Técnico" }, new[] { 3.0, 2.5, 5.5, 4.0 });
            foreach (var m in detail.Maintenances)
            {
                AddRow(table, m.MaintenanceTypeName, m.Date.ToString("yyyy-MM-dd"), m.Description, m.TechnicianName);
            }
        }

        private static void AddSpecChangeLogs(Section section, DesktopDetailViewModel detail)
        {
            AddSectionHeading(section, "Historial de cambios de especificaciones");
            if (detail.SpecChangeLogs.Count == 0)
            {
                AddEmptyNote(section, "Sin cambios registrados.");
                return;
            }

            var table = AddTable(section, new[] { "Campo", "Fecha", "Cambio", "Por" }, new[] { 3.0, 2.5, 6.0, 3.5 });
            foreach (var s in detail.SpecChangeLogs)
            {
                var change = $"{s.OldValue ?? "-"} -> {s.NewValue ?? "-"}";
                AddRow(table, s.FieldName, s.Date.ToString("yyyy-MM-dd"), change, s.ChangedByName);
            }
        }

        private static Table AddTable(Section section, string[] headers, double[] columnWidthsCm)
        {
            var table = section.AddTable();
            table.Borders.Width = Unit.FromPoint(0.5);
            table.Borders.Color = BorderColor;

            foreach (var width in columnWidthsCm)
            {
                table.AddColumn(Unit.FromCentimeter(width));
            }

            var headerRow = table.AddRow();
            headerRow.Shading.Color = HeadingBackground;
            for (var i = 0; i < headers.Length; i++)
            {
                headerRow.Cells[i].AddParagraph().AddFormattedText(headers[i], TextFormat.Bold);
            }

            return table;
        }

        private static void AddRow(Table table, params string[] values)
        {
            var row = table.AddRow();
            for (var i = 0; i < values.Length; i++)
            {
                row.Cells[i].AddParagraph(values[i]);
            }
        }
    }
}
