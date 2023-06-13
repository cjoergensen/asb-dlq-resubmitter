using Azure.Messaging.ServiceBus;
using System.Diagnostics;

// Parse command-line arguments
(string serviceBusConnectionString, string queueName, int maxConcurrentReceivers, int maxMessagesPrBatch) = ParseCommandCommandLineArgs(args);
if (string.IsNullOrWhiteSpace(serviceBusConnectionString) || string.IsNullOrWhiteSpace(queueName))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Usage: -c <connection-string> -q <queue-name> [-mcr <max-concurrent-receivers> -mmb <max-messages-per-batch>]");
    Console.ResetColor();
    return;
}

string deadLetterQueueName = queueName + "/$DeadLetterQueue";
int movedMessagesCount = 0;

Stopwatch stopwatch = new();
stopwatch.Start();

var deadLetterQueueClient = new ServiceBusClient(serviceBusConnectionString);
var mainQueueClient = new ServiceBusClient(serviceBusConnectionString);

// Start multiple receivers to move messages in parallel
Task[] tasks = new Task[maxConcurrentReceivers];
for (int i = 0; i < tasks.Length; i++)
{
    tasks[i] = MoveMessages(deadLetterQueueClient, mainQueueClient);
}
bool isRunning = true;

// Start the background thread that prints progress
Thread progressThread = new(PrintProgress);
progressThread.Start();

// Wait for all tasks to complete
await Task.WhenAll(tasks);
isRunning = false;
progressThread.Join();

await deadLetterQueueClient.DisposeAsync();
await mainQueueClient.DisposeAsync();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("");
Console.WriteLine($"Succesfully moved DLQ-messages back to main queue '{queueName}'.");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
Console.ResetColor();

(string serviceBusConnectionString, string queueName, int maxConcurrentReceivers, int maxMessagesPrBatch) ParseCommandCommandLineArgs(string[] args)
{
    string serviceBusConnectionString = "";
    string queueName = "";
    int maxConcurrentReceivers = 1;
    int maxMessagesPrBatch = 25;

    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "-c" && i + 1 < args.Length)
        {
            serviceBusConnectionString = args[i + 1];
        }
        else if (args[i] == "-q" && i + 1 < args.Length)
        {
            queueName = args[i + 1];
        }
        else if (args[i] == "-mcr" && i + 1 < args.Length)
        {
            _ = int.TryParse(args[i + 1], out maxConcurrentReceivers);
        }
        else if (args[i] == "-mmb" && i + 1 < args.Length)
        {
            _ = int.TryParse(args[i + 1], out maxMessagesPrBatch);
        }
    }

    return (serviceBusConnectionString, queueName, maxConcurrentReceivers, maxMessagesPrBatch);
}

async Task MoveMessages(ServiceBusClient receiverClient, ServiceBusClient senderClient)
{
    var deadLetterReceiver = receiverClient.CreateReceiver(deadLetterQueueName);
    var mainQueueSender = senderClient.CreateSender(queueName);

    while (true)
    {
        var receivedMessages = await deadLetterReceiver.ReceiveMessagesAsync(maxMessagesPrBatch, TimeSpan.FromMilliseconds(1000));
        if (receivedMessages.Count == 0)
        {
            await deadLetterReceiver.CloseAsync();
            await mainQueueSender.CloseAsync();
            return;
        }

        var clonedMessages = new List<ServiceBusMessage>(receivedMessages.Count);
        foreach (var receivedMessage in receivedMessages)
        {
            var clonedMessage = new ServiceBusMessage(receivedMessage.Body);
            foreach (var prop in receivedMessage.ApplicationProperties)
            {
                clonedMessage.ApplicationProperties.Add(prop.Key, prop.Value);
            }

            clonedMessages.Add(clonedMessage);
        }

        await mainQueueSender.SendMessagesAsync(clonedMessages);
        foreach (var receivedMessage in receivedMessages)
            await deadLetterReceiver.CompleteMessageAsync(receivedMessage);

        Interlocked.Add(ref movedMessagesCount, receivedMessages.Count);
    }
}

void PrintProgress()
{
    while (isRunning)
    {
        double throughput = 0;
        if (movedMessagesCount > 0 )
            throughput = movedMessagesCount / stopwatch.Elapsed.TotalSeconds;

        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Moving messages from '{deadLetterQueueName}' back to main queue '{queueName}'.");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Number of Receivers: {maxConcurrentReceivers}");
        Console.WriteLine($"Messages pr batch: {maxMessagesPrBatch}");
        Console.WriteLine("");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Elapsed Time: {GetElapsedTimeAsString()}");
        Console.WriteLine($"Number of messages moved: {movedMessagesCount:N0}");
        Console.WriteLine($"Throughput: {throughput:N0} msgs/sec");
        Console.ResetColor();

        Thread.Sleep(2000);
    }
}

string GetElapsedTimeAsString()
{
    if (stopwatch.Elapsed.TotalHours >= 1)
        return stopwatch.Elapsed.TotalHours.ToString("0.##") + " hours";
    
    if (stopwatch.Elapsed.TotalMinutes >= 1)
        return stopwatch.Elapsed.TotalMinutes.ToString("0.##") + " minutes";

    return stopwatch.Elapsed.TotalSeconds.ToString("0.##") + " seconds";
}