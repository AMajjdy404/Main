namespace Main.Core.Services
{
    public interface ISmsService
    {
        Task SendAsync(string phoneNumber, string message, string requestId, CancellationToken cancellationToken = default);
    }
}
