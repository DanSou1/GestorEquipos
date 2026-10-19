using System.Security.Cryptography;

namespace Gestor_Equipos.Services.Auth
{
    public static class TemporaryPasswordGenerator
    {
        // Sin caracteres ambiguos (0/O, 1/l/I) para que una persona pueda transcribir la
        // contraseña temporal del correo sin confundirse al iniciar sesión por primera vez.
        private const string Charset = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static string Generate(int length = 12)
        {
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = Charset[RandomNumberGenerator.GetInt32(Charset.Length)];
            }

            return new string(chars);
        }
    }
}
