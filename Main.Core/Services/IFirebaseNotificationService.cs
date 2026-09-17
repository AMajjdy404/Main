namespace Main.Core.Services
{
    public interface IFirebaseNotificationService
    {
        Task SubscribeTokenToAllClientsTopicAsync(string deviceToken, CancellationToken cancellationToken = default);
        Task<string> SendToAllClientsTopicAsync(string title, string body, CancellationToken cancellationToken = default);
    }
}
