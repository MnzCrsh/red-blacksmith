namespace RedBlacksmith.TrafficProcessing.Kafka;

internal sealed class InvalidKafkaBatchException : Exception
{
    public InvalidKafkaBatchException(string message)
        : base(message)
    {
    }

    public InvalidKafkaBatchException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}