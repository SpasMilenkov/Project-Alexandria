namespace Alexandria.Common.Exceptions.Directories;

public class InvalidDirectoryDestinationException : Exception
{
    public Guid DestinationId { get; }

    public InvalidDirectoryDestinationException(Guid destinationId)
        : base($"Directory '{destinationId}' is not a valid move destination.")
    {
        DestinationId = destinationId;
    }

    public InvalidDirectoryDestinationException(Guid destinationId, string reason)
        : base($"Directory '{destinationId}' is not a valid move destination: {reason}")
    {
        DestinationId = destinationId;
    }
}