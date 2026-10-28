namespace MessageBrokerDemo.Model.Messaging;

public enum MessageTopic
{
    JobProcess,
    JobCompletedNotification,
    JobFailedNotification
}

public static class MessageTopicExtensions
{
    public static string ToTopicName(this MessageTopic topic) => topic switch
    {
        MessageTopic.JobProcess => "job.process",
        MessageTopic.JobCompletedNotification => "job.completed.notification",
        MessageTopic.JobFailedNotification => "job.failed.notification",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
