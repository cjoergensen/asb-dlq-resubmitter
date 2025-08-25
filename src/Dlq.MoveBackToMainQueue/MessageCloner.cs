using Azure.Messaging.ServiceBus;

namespace Cjoergensen.Azure.ServiceBus.Tools.Dlq.MoveBackToMainQueue;

public static class MessageCloner
{
    public static ServiceBusMessage Clone(ServiceBusReceivedMessage source)
    {
        var clonedMessage = new ServiceBusMessage(source.Body)
        {
            ContentType = source.ContentType,
            CorrelationId = source.CorrelationId,
            Subject = source.Subject,
            MessageId = source.MessageId,
            TimeToLive = source.TimeToLive,
            SessionId = source.SessionId,
            PartitionKey = source.PartitionKey,
            ScheduledEnqueueTime = source.ScheduledEnqueueTime,
        };

        foreach (var prop in source.ApplicationProperties)
        {
            clonedMessage.ApplicationProperties.Add(prop.Key, prop.Value);
        }

        return clonedMessage;
    }
}
