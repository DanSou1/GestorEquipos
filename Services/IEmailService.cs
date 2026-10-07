namespace Gestor_Equipos.Services
{
    public record EmailAttachment(string FileName, byte[] Content, string ContentType);

    public interface IEmailService
    {
        Task SendAsync(
            string toEmail,
            string toName,
            string subject,
            string bodyHtml,
            IReadOnlyList<EmailAttachment> attachments,
            CancellationToken cancellationToken = default);
    }
}
