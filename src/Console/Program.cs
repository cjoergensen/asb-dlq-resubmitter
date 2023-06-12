using Azure.Messaging.ServiceBus;
using System.Diagnostics;

// Parse command-line arguments
(string serviceBusConnectionString, string queueName, int maxConcurrentReceivers) = ParseCommandCommandLineArgs(args);
if (string.IsNullOrWhiteSpace(serviceBusConnectionString) || string.IsNullOrWhiteSpace(queueName))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Usage: -c <connection-string> -q <queue-name> [-t <max-concurrent-receivers>]");
    Console.ResetColor();
    return;
}

string deadLetterQueueName = queueName + "/$DeadLetterQueue";
int movedMessagesCount = 0;

Stopwatch stopwatch = new();
stopwatch.Start();

var deadLetterQueueClient = new ServiceBusClient(serviceBusConnectionString);
var mainQueueClient = new ServiceBusClient(serviceBusConnectionString);

var deadLetterReceiver = deadLetterQueueClient.CreateReceiver(deadLetterQueueName);
var mainQueueSender = mainQueueClient.CreateSender(queueName);

// Start multiple threads that increment the counter asynchronously
Task[] tasks = new Task[maxConcurrentReceivers];
for (int i = 0; i < tasks.Length; i++)
{
    tasks[i] = MoveMessages(deadLetterReceiver, mainQueueSender);
}
bool isRunning = true;

// Start the background thread
Thread backgroundThread = new(PrintProgress);
backgroundThread.Start();

// Wait for all tasks to complete
await Task.WhenAll(tasks);

isRunning = false;
backgroundThread.Join();

await deadLetterQueueClient.DisposeAsync();
await mainQueueClient.DisposeAsync();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("");
Console.WriteLine($"Succesfully moved DLQ-messages back into '{queueName}'.");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

(string serviceBusConnectionString, string queueName, int maxConcurrentReceivers) ParseCommandCommandLineArgs(string[] args)
{
    string serviceBusConnectionString = "";
    string queueName = "";
    int maxConcurrentReceivers = 1;

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
        else if (args[i] == "-t" && i + 1 < args.Length)
        {
            _ = int.TryParse(args[i + 1], out maxConcurrentReceivers);
        }
    }

    return (serviceBusConnectionString, queueName, maxConcurrentReceivers);
}

async Task MoveMessages(ServiceBusReceiver deadLetterReceiver, ServiceBusSender mainQueueSender)
{
    while (true)
    {
        var receivedMessages = await deadLetterReceiver.ReceiveMessagesAsync(20);
        if (receivedMessages.Count == 0)
            return;

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
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Moving messages from '{deadLetterQueueName}' back to main queue '{queueName}'.");
        Console.WriteLine("");

        Console.WriteLine("Elapsed Time: {0}", FormatElapsedTime());
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Number of messages moved: {0}", movedMessagesCount);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Number of threads: {0}", maxConcurrentReceivers);
        Console.ResetColor();

        Thread.Sleep(2000);
    }
}

string FormatElapsedTime()
{
    if (stopwatch.Elapsed.TotalHours >= 1)
    {
        return stopwatch.Elapsed.TotalHours.ToString("0.##") + " hours";
    }
    
    if (stopwatch.Elapsed.TotalMinutes >= 1)
    {
        return stopwatch.Elapsed.TotalMinutes.ToString("0.##") + " minutes";
    }
        
    return stopwatch.Elapsed.TotalSeconds.ToString("0.##") + " seconds";
}