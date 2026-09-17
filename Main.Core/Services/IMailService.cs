using Main.Core.Models.Dtos.Mail;

namespace Main.Core.Services
{
    public interface IMailService
    {
        Task SendEmailAsync(Email email, CancellationToken cancellationToken = default);
    }
}
