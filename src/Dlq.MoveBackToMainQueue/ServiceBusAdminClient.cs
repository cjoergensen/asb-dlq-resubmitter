using Azure.Messaging.ServiceBus.Administration;

namespace Cjoergensen.Azure.ServiceBus.Tools.Dlq.MoveBackToMainQueue;

public class ServiceBusAdminClient(string connectionString)
{
    private readonly ServiceBusAdministrationClient adminClient = new(connectionString);

    public async Task<int> GetDeadLetterMessageCountAsync(string queueName)
    {
        var runtimeProperties = await adminClient.GetQueueRuntimePropertiesAsync(queueName);
        return (int)runtimeProperties.Value.DeadLetterMessageCount;
    }
    
    public async Task<int> GetSubscriptionDeadLetterMessageCountAsync(string topicName, string subscriptionName)
    {
        var runtimeProperties = await adminClient.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName);
        return (int)runtimeProperties.Value.DeadLetterMessageCount;
    } 
}
