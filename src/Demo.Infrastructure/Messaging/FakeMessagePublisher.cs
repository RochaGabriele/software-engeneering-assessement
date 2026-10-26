using Demo.Domain.Common.Services;
using Microsoft.Extensions.Logging;

namespace Demo.Infrastructure.Messaging;

public class FakeMessagePublisher : IMessagePublisher
{
    private readonly ILogger<FakeMessagePublisher> _logger;

    public FakeMessagePublisher(ILogger<FakeMessagePublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class
    {
        _logger.LogWarning("Message {MessageType} should be published to a real bus. Currently using FakeMessagePublisher.", typeof(T).Name);
        return Task.CompletedTask;
    }
}
