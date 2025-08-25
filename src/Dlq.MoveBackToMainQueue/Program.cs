using Cjoergensen.Azure.ServiceBus.Tools.Dlq.MoveBackToMainQueue;

var arguments = CommandLineArgumentsParser.ParseArguments(args);
if (arguments.ShowHelp)
{
    ConsoleUI.PrintUsageInstructions(arguments.ErrorMessage);
    return;
}

var sbAdminClient = new ServiceBusAdminClient(arguments.ServiceBusConnectionString!);
var totalNumberOfDeadLetterMessages = await sbAdminClient.GetSubscriptionDeadLetterMessageCountAsync("legacy", arguments.QueueName!);

var consoleUI = new ConsoleUI(arguments, totalNumberOfDeadLetterMessages);
consoleUI.PrintHeader();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    ConsoleUI.PrintCancellationMessage();
    cts.Cancel();
    e.Cancel = true;
};

var totalMoved = 0;
var totalFailed = 0;

var progressTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        consoleUI.UpdateProgress(totalMoved, totalFailed);
        await Task.Delay(1000, cts.Token);
    }
}, cts.Token);

var tasks = new List<Task>(arguments.MaxConcurrency);
try
{
    for (int i = 0; i < arguments.MaxConcurrency; i++)
    {
        tasks.Add(
            Task.Run(async () =>
            {
                try
                {
                    var mover = new DeadLetterMessageMover(arguments.ServiceBusConnectionString!, arguments.QueueName!, arguments.Identifier);
                    await foreach (var result in mover.MoveMessagesAsync(cts.Token))
                    {
                        if (result.IsSuccess)
                        {
                            Interlocked.Increment(ref totalMoved);
                        }
                        else
                        {
                            Interlocked.Increment(ref totalFailed);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Gracefully handle cancellation
                }
            }, cts.Token));
    }

    await Task.WhenAll(tasks);
}
catch (OperationCanceledException)
{
    // Gracefully handle cancellation
}
finally
{
    cts.Cancel();
    try
    {
        await progressTask;
    }
    catch (OperationCanceledException)
    {
        // Ignore cancellation of progress task
    }
    consoleUI.PrintSummary();
}