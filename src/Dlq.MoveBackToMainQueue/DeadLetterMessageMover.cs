using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using FluentResults;

namespace Cjoergensen.Azure.ServiceBus.Tools.Dlq.MoveBackToMainQueue;

public class DeadLetterMessageMover(string connectionString, string queueName, string identifier = "")
{
    private readonly ServiceBusClient client = new(connectionString);

    public async IAsyncEnumerable<FluentResults.Result> MoveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var sender = client.CreateSender(queueName, new ServiceBusSenderOptions
            {
                Identifier = identifier
            });
            

            // await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions
            // {
            //     SubQueue = SubQueue.DeadLetter,
            //     ReceiveMode = ServiceBusReceiveMode.PeekLock,
            //     Identifier = identifier
            // });

            await using var receiver = client.CreateReceiver("legacy", queueName, new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                Identifier = identifier
            });
            
            await foreach (var msg in receiver.ReceiveMessagesAsync(cancellationToken))
            {
                if(msg == null)
                {
                    yield break; // No more messages to process
                }
                
                var result = Result.Ok();
                try
                {
                    await sender.SendMessageAsync(MessageCloner.Clone(msg), cancellationToken);
                    await receiver.CompleteMessageAsync(msg, cancellationToken);
                }
                catch
                {
                    result = Result.Fail("Failed to complete message: " + msg.MessageId);
                }

                yield return result;
            }
        }
    }
}