using Google.Protobuf.WellKnownTypes;

namespace RedBlacksmith.TrafficIngestion.Application;

internal static class TimeStampExtensions
{
    private const long MinSeconds = -62135596800;
    private const long MaxSeconds = 253402300799;
    
    private const int MinNanos = 0;
    private const int MaxNanos = 999999999;

    public static bool IsValid(this Timestamp? timestamp)
    {
        if (timestamp is null) 
            return false;

        return timestamp.Seconds is >= MinSeconds and <= MaxSeconds &&
               timestamp.Nanos is >= MinNanos and <= MaxNanos;
    }
}