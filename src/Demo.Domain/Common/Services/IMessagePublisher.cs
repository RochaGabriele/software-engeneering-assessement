namespace Demo.Domain.Common.Services
{
    public interface IMessagePublisher
    {
        Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class;
    }
}
