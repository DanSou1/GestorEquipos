using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Maintenance;
using Microsoft.AspNetCore.Hosting;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Gestor_Equipos.Services.Implementations
{
    public class MaintenancePdfService : IMaintenancePdfService
    {
        private static readonly Color HeadingBackground = new(248, 249, 250);
        private static readonly Color BorderColor = new(222, 226, 230);
        private static readonly Color MutedColor = new(108, 117, 125);
        private static readonly Color ActiveColor = new(25, 135, 84);

        private readonly IWebHostEnvironment _env;

        static MaintenancePdfService()
        {
            GlobalFontSettings.FontResolver ??= new LocalFontResolver();
        }

        public MaintenancePdfService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public byte[] GenerateMaintenancePdf(MaintenancePdfViewModel detail)
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
            AddSectionHeading(section, "Mantenimiento");
            AddLabelValue(section, "Fecha de mantenimiento", detail.Date.ToString("yyyy-MM-dd"));
            AddLabelValue(section, "Técnico", detail.TechnicianName);
            AddLabelValue(section, "Descripción", detail.Description);

            if (detail.Category == MaintenanceCategory.Preventivo)
            {
                AddSectionHeading(section, "Usuario y área");
                AddLabelValue(section, "Usuario", detail.CurrentUserName);
                AddLabelValue(section, "Área", detail.CurrentAreaName);

                AddEquipment(section, detail);
                AddChecklist(section, detail);
                AddSignatureBlocks(section);
            }

            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();

            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return stream.ToArray();
        }

        // Acta en el formato oficial de la empresa ("E-FGA-07 Control Mantenimiento V04"),
        // la misma plantilla que ya se llenaba a mano por fuera del sistema. Usado solo por
        // el módulo de Mantenimientos obligatorios (junio/diciembre).
        public byte[] GenerateExroActaPdf(MaintenanceExroActaViewModel detail)
        {
            var document = new Document();
            DefineStyles(document);

            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(2);
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);

            AddExroHeader(section, _env);
            AddExroDatosGenerales(section, detail);
            AddExroEquipmentTable(section, detail);
            AddExroDescripcionFalla(section, detail);
            AddExroProcedimientoTable(section, detail);
            AddExroProcedimientoFijo(section);
            AddExroFirmas(section, detail);

            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();

            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return stream.ToArray();
        }

        private static void AddExroHeader(Section section, IWebHostEnvironment env)
        {
            var headerTable = section.AddTable();
            headerTable.Borders.Width = 0;
            headerTable.AddColumn(Unit.FromCentimeter(6));
            headerTable.AddColumn(Unit.FromCentimeter(7));
            headerTable.AddColumn(Unit.FromCentimeter(4));

            var row = headerTable.AddRow();
            row.VerticalAlignment = VerticalAlignment.Center;

            var logoPath = Path.Combine(env.WebRootPath, "Images", "logo-exro.png");
            if (File.Exists(logoPath))
            {
                var image = row.Cells[0].AddParagraph().AddImage(logoPath);
                image.Width = Unit.FromCentimeter(5);
                image.LockAspectRatio = true;
            }

            var title = row.Cells[1].AddParagraph("CONTROL MANTENIMIENTO");
            title.Format.Font.Size = 14;
            title.Format.Font.Bold = true;
            title.Format.Alignment = ParagraphAlignment.Center;

            var codeCell = row.Cells[2];
            codeCell.Borders.Width = Unit.FromPoint(0.75);
            codeCell.Borders.Color = BorderColor;
            AddSmallLine(codeCell, "Código: E-FGA-07");
            AddSmallLine(codeCell, "Versión: 04");
            AddSmallLine(codeCell, "Vigente desde: 10/06/2022");
        }

        private static void AddSmallLine(Cell cell, string text)
        {
            var paragraph = cell.AddParagraph(text);
            paragraph.Format.Font.Size = 8;
        }

        private static void AddExroDatosGenerales(Section section, MaintenanceExroActaViewModel detail)
        {
            var table = section.AddTable();
            table.Format.SpaceBefore = Unit.FromPoint(10);
            table.Borders.Width = Unit.FromPoint(0.5);
            table.Borders.Color = BorderColor;
            table.AddColumn(Unit.FromCentimeter(4));
            table.AddColumn(Unit.FromCentimeter(13));

            AddLabelRow(table, "FECHA:", detail.Date.ToString("dd/MM/yyyy"));
            AddLabelRow(table, "USUARIO:", detail.UsuarioNombre);
            AddLabelRow(table, "ÁREA:", detail.Area);
        }

        private static void AddLabelRow(Table table, string label, string value)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph().AddFormattedText(label, TextFormat.Bold);
            row.Cells[1].AddParagraph(value);
        }

        private static void AddExroEquipmentTable(Section section, MaintenanceExroActaViewModel detail)
        {
            var table = AddTable(section, new[] { "Equipo", "Marca", "Modelo", "Serial" }, new[] { 3.5, 3.5, 5.0, 5.0 });
            foreach (var e in detail.Equipment)
            {
                AddRow(table, e.Label, e.Brand, e.Model, e.Serial ?? "N/A");
            }
        }

        private static void AddExroDescripcionFalla(Section section, MaintenanceExroActaViewModel detail)
        {
            AddSectionHeading(section, "Descripción falla");
            section.AddParagraph(detail.DescripcionFalla);
        }

        private static void AddExroProcedimientoTable(Section section, MaintenanceExroActaViewModel detail)
        {
            AddSectionHeading(section, "Procedimiento realizado");

            var table = section.AddTable();
            table.Borders.Width = Unit.FromPoint(0.5);
            table.Borders.Color = BorderColor;
            for (var i = 0; i < 8; i++)
            {
                table.AddColumn(Unit.FromCentimeter(2.125));
            }

            var groupRow = table.AddRow();
            groupRow.Shading.Color = HeadingBackground;
            AddGroupHeader(groupRow, 0, 3, "Inspección técnica");
            AddGroupHeader(groupRow, 3, 2, "Limpieza");
            AddGroupHeader(groupRow, 5, 3, "Mantenimiento");

            var labelRow = table.AddRow();
            labelRow.Shading.Color = HeadingBackground;
            var valueRow = table.AddRow();

            for (var i = 0; i < detail.Checklist.Count && i < 8; i++)
            {
                var labelParagraph = labelRow.Cells[i].AddParagraph(detail.Checklist[i].Label);
                labelParagraph.Format.Font.Size = 7;
                labelParagraph.Format.Alignment = ParagraphAlignment.Center;

                var valueParagraph = valueRow.Cells[i].AddParagraph(detail.Checklist[i].Checked ? "X" : "N/A");
                valueParagraph.Format.Font.Bold = true;
                valueParagraph.Format.Alignment = ParagraphAlignment.Center;
            }
        }

        private static void AddGroupHeader(Row row, int startIndex, int span, string text)
        {
            var cell = row.Cells[startIndex];
            cell.MergeRight = span - 1;
            var paragraph = cell.AddParagraph();
            paragraph.AddFormattedText(text, TextFormat.Bold);
            paragraph.Format.Alignment = ParagraphAlignment.Center;
        }

        private static void AddExroProcedimientoFijo(Section section)
        {
            AddSectionHeading(section, "Procedimiento a seguir");
            var paragraph = section.AddParagraph("El equipo se entrega operativo");
            paragraph.Format.Alignment = ParagraphAlignment.Center;
        }

        private static void AddExroFirmas(Section section, MaintenanceExroActaViewModel detail)
        {
            AddSectionHeading(section, "Firmas");

            var table = section.AddTable();
            table.Borders.Width = 0;
            table.AddColumn(Unit.FromCentimeter(8.5));
            table.AddColumn(Unit.FromCentimeter(0.5));
            table.AddColumn(Unit.FromCentimeter(8.5));

            var spaceRow = table.AddRow();
            spaceRow.Height = Unit.FromCentimeter(1.5);
            spaceRow.Cells[0].Borders.Bottom.Width = Unit.FromPoint(0.75);
            spaceRow.Cells[0].Borders.Bottom.Color = BorderColor;
            spaceRow.Cells[2].Borders.Bottom.Width = Unit.FromPoint(0.75);
            spaceRow.Cells[2].Borders.Bottom.Color = BorderColor;

            var labelRow = table.AddRow();
            labelRow.Format.SpaceBefore = Unit.FromPoint(4);
            labelRow.Cells[0].AddParagraph("Realizado por");
            var technicianName = labelRow.Cells[0].AddParagraph(detail.TechnicianName);
            technicianName.Format.Font.Size = 8;
            technicianName.Format.Font.Italic = true;
            labelRow.Cells[2].AddParagraph("Nombre de la persona que recibe a conformidad");
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

        private static void AddHeader(Section section, MaintenancePdfViewModel detail)
        {
            var title = section.AddParagraph(detail.DesktopName);
            title.Style = "Heading1";

            var meta = section.AddParagraph();
            meta.Format.SpaceAfter = Unit.FromPoint(4);
            meta.AddFormattedText("Categoría: ", TextFormat.Bold);
            var badge = meta.AddFormattedText(detail.Category == MaintenanceCategory.Preventivo ? "Preventivo" : "Correctivo");
            badge.Bold = true;
            badge.Color = detail.Category == MaintenanceCategory.Preventivo ? ActiveColor : MutedColor;
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

        private static void AddEquipment(Section section, MaintenancePdfViewModel detail)
        {
            AddSectionHeading(section, "Equipo y periféricos");
            if (detail.Equipment.Count == 0)
            {
                AddEmptyNote(section, "Sin equipo o periféricos registrados.");
                return;
            }

            var table = AddTable(section, new[] { "Elemento", "Marca", "Modelo", "Serial" }, new[] { 3.5, 3.5, 4.0, 4.0 });
            foreach (var e in detail.Equipment)
            {
                AddRow(table, e.Label, e.Brand, e.Model, e.Serial ?? "-");
            }
        }

        private static void AddChecklist(Section section, MaintenancePdfViewModel detail)
        {
            AddSectionHeading(section, "Checklist de mantenimiento preventivo");
            if (detail.Checklist is null || detail.Checklist.Count == 0)
            {
                AddEmptyNote(section, "Sin checklist registrado.");
                return;
            }

            var table = AddTable(section, new[] { "Actividad", "Estado" }, new[] { 12.0, 3.0 });
            foreach (var item in detail.Checklist)
            {
                AddRow(table, item.Label, item.Checked ? "X" : "N/A");
            }
        }

        private static void AddSignatureBlocks(Section section)
        {
            AddSectionHeading(section, "Firmas");

            var table = section.AddTable();
            table.Borders.Width = 0;
            table.AddColumn(Unit.FromCentimeter(8.5));
            table.AddColumn(Unit.FromCentimeter(0.5));
            table.AddColumn(Unit.FromCentimeter(8.5));

            var spaceRow = table.AddRow();
            spaceRow.Height = Unit.FromCentimeter(1.5);
            spaceRow.Cells[0].Borders.Bottom.Width = Unit.FromPoint(0.75);
            spaceRow.Cells[0].Borders.Bottom.Color = BorderColor;
            spaceRow.Cells[2].Borders.Bottom.Width = Unit.FromPoint(0.75);
            spaceRow.Cells[2].Borders.Bottom.Color = BorderColor;

            var labelRow = table.AddRow();
            labelRow.Format.SpaceBefore = Unit.FromPoint(4);
            labelRow.Cells[0].AddParagraph("Realizado por");
            labelRow.Cells[2].AddParagraph("Nombre de la persona que recibe a conformidad");
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
