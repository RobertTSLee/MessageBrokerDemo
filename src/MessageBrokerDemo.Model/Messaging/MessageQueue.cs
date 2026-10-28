namespace MessageBrokerDemo.Model.Messaging;

public enum MessageQueue
{
    ProcessJob
}

public static class MessageQueueExtensions
{
    public static string ToQueueName(this MessageQueue queue) => queue switch
    {
        MessageQueue.ProcessJob => "process-job",
        _ => throw new ArgumentOutOfRangeException(nameof(queue), queue, null)
    };
}
