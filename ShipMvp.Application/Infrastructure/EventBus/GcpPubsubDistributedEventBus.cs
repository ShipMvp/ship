using System;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipMvp.Core.EventBus;
using ShipMvp.Application.Infrastructure.Gcp;
using Grpc.Auth;
using Google.Apis.Auth.OAuth2;

namespace ShipMvp.Application.Infrastructure.EventBus;

public class GcpPubsubDistributedEventBus : IDistributedEventBus
{
    private readonly string _projectId;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly PublisherServiceApiClient _publisherService;
    private readonly SubscriberServiceApiClient _subscriberService;
    private readonly GoogleCredential _credential;

    public GcpPubsubDistributedEventBus(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _projectId = configuration["Gcp:ProjectId"] ?? throw new ArgumentNullException("Gcp:ProjectId");
        _credential = GcpCredentialFactory.Create(_configuration);
        _publisherService = new PublisherServiceApiClientBuilder
        {
            ChannelCredentials = _credential.ToChannelCredentials()
        }.Build();
        _subscriberService = new SubscriberServiceApiClientBuilder
        {
            ChannelCredentials = _credential.ToChannelCredentials()
        }.Build();
    }

    private string GetSharedTopicId() =>
        _configuration["Gcp:PubSub:TopicId"] ?? "SnapshotMeta";

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : class
    {
        var topicName = TopicName.FromProjectTopic(_projectId, GetSharedTopicId());

        await EnsureTopicExistsAsync(topicName);

        var builder = new PublisherClientBuilder
        {
            ChannelCredentials = _credential.ToChannelCredentials(),
            TopicName = topicName
        };
        var publisher = await builder.BuildAsync();

        // Wrap event with type info
        var envelope = new EventEnvelope
        {
            EventType = typeof(TEvent).FullName!,
            Data = JsonSerializer.SerializeToElement(@event)
        };
        var json = JsonSerializer.Serialize(envelope);
        await publisher.PublishAsync(json);
    }

    public async Task SubscribeAsync<TEvent, THandler>()
        where TEvent : class
        where THandler : IDistributedEventHandler<TEvent>
    {
        var subscriptionId = typeof(THandler).Name;
        var topicName = TopicName.FromProjectTopic(_projectId, GetSharedTopicId());
        var subscriptionName = SubscriptionName.FromProjectSubscription(_projectId, subscriptionId);

        await EnsureTopicExistsAsync(topicName);

        // Ensure subscription exists
        try
        {
            await _subscriberService.GetSubscriptionAsync(subscriptionName);
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.NotFound)
        {
            await _subscriberService.CreateSubscriptionAsync(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 60);
        }

        var builder = new SubscriberClientBuilder
        {
            ChannelCredentials = _credential.ToChannelCredentials(),
            SubscriptionName = subscriptionName
        };
        var subscriber = await builder.BuildAsync();
        _ = subscriber.StartAsync(async (msg, ct) =>
        {
            // Deserialize envelope and filter by event type
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(msg.Data.ToStringUtf8());
            if (envelope != null && envelope.EventType == typeof(TEvent).FullName)
            {
                var evt = envelope.Data.Deserialize<TEvent>();
                if (evt != null)
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var handler = (THandler?)scope.ServiceProvider.GetService(typeof(THandler));
                        if (handler != null)
                        {
                            await handler.HandleAsync(evt);
                        }
                    }
                }
            }
            return SubscriberClient.Reply.Ack;
        });
    }

    private async Task EnsureTopicExistsAsync(TopicName topicName)
    {
        try
        {
            await _publisherService.GetTopicAsync(topicName);
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.NotFound)
        {
            await _publisherService.CreateTopicAsync(topicName);
        }
    }

    private class EventEnvelope
    {
        public string EventType { get; set; } = string.Empty;
        public JsonElement Data { get; set; }
    }
}
