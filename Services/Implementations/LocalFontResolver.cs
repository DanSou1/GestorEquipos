using PdfSharp.Fonts;

namespace Gestor_Equipos.Services.Implementations
{
    // En Windows, Environment.SpecialFolder.Fonts resuelve a C:\Windows\Fonts con los
    // nombres de archivo clásicos (arial.ttf, cour.ttf, ...). En Linux (contenedor Docker)
    // ese special folder no existe; el Dockerfile instala en su lugar las fuentes libres
    // metric-compatible "Liberation" (sustituto de Arial/Times New Roman/Courier New) y
    // "DejaVu Sans" (sustituto de Verdana, la única familia que la app usa realmente hoy —
    // ver MaintenancePdfService/DesktopPdfService) desde los repos main de Debian, sin
    // depender de ttf-mscorefonts-installer (vive en "contrib", requiere aceptar EULA y
    // descarga los .ttf reales de Microsoft desde sourceforge en build time, poco fiable).
    internal sealed class LocalFontResolver : IFontResolver
    {
        private static readonly IReadOnlyList<string> FontDirectories = BuildFontDirectories();

        private static readonly IReadOnlyDictionary<string, string[]> FontFileAliases = new Dictionary<string, string[]>
        {
            ["cour.ttf"] = new[] { "cour.ttf", "LiberationMono-Regular.ttf" },
            ["courbd.ttf"] = new[] { "courbd.ttf", "LiberationMono-Bold.ttf" },
            ["couri.ttf"] = new[] { "couri.ttf", "LiberationMono-Italic.ttf" },
            ["courbi.ttf"] = new[] { "courbi.ttf", "LiberationMono-BoldItalic.ttf" },
            ["times.ttf"] = new[] { "times.ttf", "LiberationSerif-Regular.ttf" },
            ["timesbd.ttf"] = new[] { "timesbd.ttf", "LiberationSerif-Bold.ttf" },
            ["timesi.ttf"] = new[] { "timesi.ttf", "LiberationSerif-Italic.ttf" },
            ["timesbi.ttf"] = new[] { "timesbi.ttf", "LiberationSerif-BoldItalic.ttf" },
            ["arial.ttf"] = new[] { "arial.ttf", "LiberationSans-Regular.ttf" },
            ["arialbd.ttf"] = new[] { "arialbd.ttf", "LiberationSans-Bold.ttf" },
            ["ariali.ttf"] = new[] { "ariali.ttf", "LiberationSans-Italic.ttf" },
            ["arialbi.ttf"] = new[] { "arialbi.ttf", "LiberationSans-BoldItalic.ttf" },
            ["verdana.ttf"] = new[] { "verdana.ttf", "DejaVuSans.ttf" },
            ["verdanab.ttf"] = new[] { "verdanab.ttf", "DejaVuSans-Bold.ttf" },
            ["verdanai.ttf"] = new[] { "verdanai.ttf", "DejaVuSans-Oblique.ttf" },
            ["verdanaz.ttf"] = new[] { "verdanaz.ttf", "DejaVuSans-BoldOblique.ttf" },
        };

        public byte[]? GetFont(string faceName)
        {
            var candidates = FontFileAliases.TryGetValue(faceName, out var aliases) ? aliases : new[] { faceName };

            foreach (var directory in FontDirectories)
            {
                foreach (var candidate in candidates)
                {
                    var path = Path.Combine(directory, candidate);
                    if (File.Exists(path))
                    {
                        return File.ReadAllBytes(path);
                    }
                }
            }

            return null;
        }

        private static IReadOnlyList<string> BuildFontDirectories()
        {
            var directories = new List<string>();

            var windowsFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (!string.IsNullOrEmpty(windowsFonts))
            {
                directories.Add(windowsFonts);
            }

            directories.Add("/usr/share/fonts/truetype/liberation");
            directories.Add("/usr/share/fonts/truetype/dejavu");

            return directories;
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var (regular, bold, italic, boldItalic) = familyName switch
            {
                "Courier New" => ("cour.ttf", "courbd.ttf", "couri.ttf", "courbi.ttf"),
                "Times New Roman" => ("times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf"),
                "Arial" => ("arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"),
                _ => ("verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf")
            };

            var fileName = (isBold, isItalic) switch
            {
                (true, true) => boldItalic,
                (true, false) => bold,
                (false, true) => italic,
                _ => regular
            };

            return new FontResolverInfo(fileName);
        }
    }
}
