namespace Cjoergensen.Azure.ServiceBus.Tools.Dlq.MoveBackToMainQueue;

public class ConsoleUI(CommandLineArgumentsParser.ArgumentParsingResult arguments, int totalNumberOfDeadLetterMessages = 0)
{
    private readonly DateTime startTime = DateTime.Now;
    private int totalMoved;
    private int totalFailed;

    public void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=================================================");
        Console.WriteLine("  Azure Service Bus DLQ Message Mover");
        Console.WriteLine("=================================================");
        Console.ResetColor();
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Queue Name: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(arguments.QueueName);
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Total Dead Letter Messages: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{totalNumberOfDeadLetterMessages:N0}");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Concurrent Tasks: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{arguments.MaxConcurrency}");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Start Time: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{startTime:dd-MM-yyyy HH:mm:ss}");
        
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("Press Ctrl+C to stop processing");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("-------------------------------------------------\n");
        Console.ResetColor();
    }

    public void UpdateProgress(int moved, int failed)
    {
        totalMoved = moved;
        totalFailed = failed;
        PrintProgress();
    }

    private void PrintProgress()
    {
        var elapsed = DateTime.Now - startTime;
        var messagesPerSecond = elapsed.TotalSeconds > 0 
            ? Math.Round(totalMoved / elapsed.TotalSeconds, 1) 
            : 0;

        var remainingMessages = totalNumberOfDeadLetterMessages - totalMoved;
        var estimatedSecondsRemaining = messagesPerSecond > 0 
            ? remainingMessages / messagesPerSecond 
            : 0;

        var estimatedCompletionTime = DateTime.Now.AddSeconds(estimatedSecondsRemaining);

        // If no messages have been processed yet or if processing is too slow,
        // use a more conservative estimate based on concurrency
        if (messagesPerSecond < 0.1 && arguments.MaxConcurrency > 0)
        {
            // Assume each concurrent task can process at least 1 message per 2 seconds
            var estimatedMinimumThroughput = arguments.MaxConcurrency / 2.0;
            estimatedSecondsRemaining = remainingMessages / estimatedMinimumThroughput;
            estimatedCompletionTime = DateTime.Now.AddSeconds(estimatedSecondsRemaining);
        }

        Console.Write("\r");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Processed: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{totalMoved:N0} messages ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"({messagesPerSecond} msg/s), ");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Failed: ");
        Console.ForegroundColor = totalFailed > 0 ? ConsoleColor.Red : ConsoleColor.Green;
        Console.Write($"{totalFailed:N0}, ");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Elapsed: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($@"{elapsed:hh\:mm\:ss}, ");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Est. Remaining: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($@"{TimeSpan.FromSeconds(estimatedSecondsRemaining):hh\:mm\:ss}, ");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("ETA: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{estimatedCompletionTime:HH:mm:ss}");
        Console.ResetColor();
    }

    public void PrintSummary()
    {
        var endTime = DateTime.Now;
        var duration = endTime - startTime;
        var avgMessagesPerSecond = duration.TotalSeconds > 0 
            ? Math.Round(totalMoved / duration.TotalSeconds, 1) 
            : 0;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("-------------------------------------------------\n");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Completed at: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{endTime:dd-MM-yyyy HH:mm:ss}");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Messages Moved: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{totalMoved:N0} of {totalNumberOfDeadLetterMessages:N0}");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Average Speed: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{avgMessagesPerSecond:N1} msg/s");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("Duration: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($@"{duration:hh\:mm\:ss}");
        
        if (totalFailed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Messages Failed: {totalFailed:N0} ({(totalFailed * 100.0 / (totalMoved + totalFailed)):N1}%)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("All messages processed successfully.");
        }
        Console.ResetColor();
    }

    public static void PrintCancellationMessage()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Cancellation requested...");
        Console.ResetColor();
    }

    public static void PrintUsageInstructions(string? errorMessage = null)
    {
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {errorMessage}");
            Console.WriteLine();
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Usage:");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Required parameters:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  -c  <connection-string>      Azure Service Bus connection string");
        Console.WriteLine("  -q  <queue-name>             Name of the queue to process");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Optional parameters:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  -mc <max-concurrency>        Maximum number of concurrent operations (default: {Constants.DefaultMaxConcurrency}, max: {Constants.MaxAllowedConcurrency})");
        Console.WriteLine($"  -i  <identifier>             A property used to set the ServiceBusReceiver ID to identify the client. If null or empty, a random unique value will be used. (default: {Constants.DefaultIdentifier})");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Help:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  -h, --help                   Show this help message");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Example:");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  DlqMover -c \"Endpoint=sb://...\" -q \"myqueue\" -mc 16 -i \"CustomMove2024\"");
        Console.ResetColor();
    }
}