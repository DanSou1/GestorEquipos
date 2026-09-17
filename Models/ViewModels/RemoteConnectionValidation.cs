namespace GestorEquipos.Models.ViewModels
{
    // Shared between DesktopCreateViewModel (NewRemote* fields) and RemoteEditViewModel — both
    // used to validate IP/port only by shape (regex), which accepted values like "999.999.999.999"
    // or port "99999" (see LOG-10 in REVISION_PROYECTO.md).
    internal static class RemoteConnectionValidation
    {
        public static bool IsValidIPv4(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (!byte.TryParse(part, out _))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsValidPort(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && int.TryParse(value, out var port)
                && port is >= 1 and <= 65535;
        }
    }
}
