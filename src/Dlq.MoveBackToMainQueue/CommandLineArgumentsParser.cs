namespace Cjoergensen.Azure.ServiceBus.Tools.Dlq.MoveBackToMainQueue;

public static class CommandLineArgumentsParser
{
    public class ArgumentParsingResult
    {
        public bool ShowHelp { get;init; }
        public string? ErrorMessage { get; init; }
        public string? ServiceBusConnectionString { get; init; }
        public string? QueueName { get; init; }
        public int MaxConcurrency { get; init; }
        public string Identifier { get; init; } = "";
    }

    public static ArgumentParsingResult ParseArguments(string[] args)
    {
        // Check for help request
        if (args.Length == 0 || args.Any(arg => arg.ToLowerInvariant() is "-h" or "--help"))
        {
            return new ArgumentParsingResult { ShowHelp = true };
        }

        var serviceBusConnectionString = string.Empty;
        var queueName = string.Empty;
        var maxConcurrency = Constants.DefaultMaxConcurrency;
        string? providedIdentifier = null;

        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-c" when i + 1 < args.Length:
                        serviceBusConnectionString = args[++i];
                        break;
                    case "-q" when i + 1 < args.Length:
                        queueName = args[++i];
                        break;
                    case "-mc" when i + 1 < args.Length:
                        maxConcurrency = int.Parse(args[++i]);
                        break;
                    case "-i" when i + 1 < args.Length:
                        providedIdentifier = args[++i];
                        break;
                }
            }

            ValidateArguments(serviceBusConnectionString, queueName, maxConcurrency);
        }
        catch (ArgumentException ex)
        {
            return new ArgumentParsingResult { ShowHelp = true, ErrorMessage = ex.Message };
        }
        catch (FormatException)
        {
            return new ArgumentParsingResult { ShowHelp = true, ErrorMessage = "Invalid max concurrency value: Must be a number." };
        }

        var identifier = !string.IsNullOrWhiteSpace(providedIdentifier) ? providedIdentifier : Constants.DefaultIdentifier;
        
        return new ArgumentParsingResult
        {
            ShowHelp = false,
            ServiceBusConnectionString = serviceBusConnectionString,
            QueueName = queueName,
            MaxConcurrency = maxConcurrency,
            Identifier = identifier
        };
    }
    
    private static void ValidateArguments(string connectionString, string queueName, int maxConcurrency)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Service Bus connection string is required.");
        }

        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Queue name is required.");
        }

        if (!connectionString.Contains("Endpoint=") || !connectionString.Contains("SharedAccessKey="))
        {
            throw new ArgumentException("Invalid Service Bus connection string format.");
        }

        if (maxConcurrency <= 0)
        {
            throw new ArgumentException("Max concurrency must be a positive number.");
        }

        if (maxConcurrency > Constants.MaxAllowedConcurrency)
        {
            throw new ArgumentException($"Max concurrency cannot exceed {Constants.MaxAllowedConcurrency}.");
        }
    }
}