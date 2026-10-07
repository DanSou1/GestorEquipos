using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Gestor_Equipos.Services.Implementations
{
    // Envío vía Gmail SMTP con contraseña de aplicación (gratuito, sin cuota relevante para
    // el volumen de este módulo — dos campañas al año). Credenciales en configuración:
    // "Smtp:Host"/"Smtp:Port"/"Smtp:FromAddress"/"Smtp:FromName" en appsettings.json,
    // "Smtp:Password" solo en user-secrets (dev) o la variable Smtp__Password (Docker) —
    // nunca en appsettings*.json, mismo criterio que BootstrapAdmin:Password.
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public SmtpEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendAsync(
            string toEmail,
            string toName,
            string subject,
            string bodyHtml,
            IReadOnlyList<EmailAttachment> attachments,
            CancellationToken cancellationToken = default)
        {
            var host = _configuration["Smtp:Host"];
            var fromAddress = _configuration["Smtp:FromAddress"];
            var password = _configuration["Smtp:Password"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "El envío de correo no está configurado (falta Smtp:Host, Smtp:FromAddress o Smtp:Password).");
            }

            var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
            var fromName = _configuration["Smtp:FromName"] ?? fromAddress;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromAddress));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = bodyHtml };
            foreach (var attachment in attachments)
            {
                bodyBuilder.Attachments.Add(attachment.FileName, attachment.Content, MimeKit.ContentType.Parse(attachment.ContentType));
            }
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(fromAddress, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
