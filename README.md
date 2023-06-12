# asb-dlq-resubmitter
Tool for resubmitting messages stuck in Azure Service Bus DLQ.

Usage:

```
.\asb-dlq-resubmit.exe -c <connection-string> -q <queue-name> [-mcr <max-concurrent-receivers> -mmb <max-messages-per-batch>]
```

## Connection string

The connection string for the Service Bus namespace.

## Queue name

The name of the queue to resubmit messages to. The DLQ will be automatically detected based on this value using this pattern: `<queue-name>/$DeadLetterQueue`.


## Max concurrent receivers

The default value for the maximum number of concurrent receivers is 1. This means that only one reciever will be processing at a time. 

If you want to increase the number of concurrent receivers, you can use the `-mcr` parameter.

## Max messages per batch

The default value for the maximum number of messages per batch is 25. This means that each receiver will attempt to receive and re-submit 25 messages per batch. 

If you want to increase the number of messages per batch, you can use the `-mmb` parameter.