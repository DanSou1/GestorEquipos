using PdfSharp.Fonts;

namespace Gestor_Equipos.Services.Implementations
{
    internal sealed class LocalFontResolver : IFontResolver
    {
        private static readonly string FontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        public byte[]? GetFont(string faceName)
        {
            var path = Path.Combine(FontsDirectory, faceName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
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
