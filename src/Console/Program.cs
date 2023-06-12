// See https://aka.ms/new-console-template for more information
using Azure.Messaging.ServiceBus;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;


const string ServiceBusConnectionString = "";
const string QueueName = "";
const string DeadLetterQueueName = QueueName + "/$DeadLetterQueue";
const int MaxConcurrentReceivers = 10; // Maximum number of concurrent receivers

Stopwatch stopwatch = new();
stopwatch.Start();


var deadLetterQueueClient = new ServiceBusClient(ServiceBusConnectionString);
var mainQueueClient = new ServiceBusClient(ServiceBusConnectionString);

var deadLetterReceiver = deadLetterQueueClient.CreateReceiver(DeadLetterQueueName);
var mainQueueSender = mainQueueClient.CreateSender(QueueName);


int movedMessagesCount = 0;


// Start multiple threads that increment the counter asynchronously
Task[] tasks = new Task[MaxConcurrentReceivers];
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

Console.Clear();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"DONE moving messages from '{DeadLetterQueueName}' back to main queue '{QueueName}'.");
Console.WriteLine("");

Console.WriteLine("Elapsed Time: {0}", GetElapsedTime());
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("Number of messages moved: {0}", movedMessagesCount);
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("Number of threads: {0}", MaxConcurrentReceivers);
Console.ResetColor();

Console.WriteLine("Press any key to exit...");
Console.ReadKey();


void PrintProgress()
{
    while (isRunning)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Moving messages from '{DeadLetterQueueName}' back to main queue '{QueueName}'.");
        Console.WriteLine("");

        Console.WriteLine("Elapsed Time: {0}", GetElapsedTime());
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Number of messages moved: {0}", movedMessagesCount);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Number of threads: {0}", MaxConcurrentReceivers);
        Console.ResetColor();

        Thread.Sleep(2000);
    }
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

string GetElapsedTime()
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