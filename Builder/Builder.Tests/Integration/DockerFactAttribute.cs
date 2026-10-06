using System.Runtime.CompilerServices;

namespace Builder.Tests.Integration;

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("ALEXANDRIA_BUILDER_DOCKER_TESTS") != "1")
        {
            Skip = "Set ALEXANDRIA_BUILDER_DOCKER_TESTS=1 to run isolated Docker integration tests using cached images.";
        }
    }
}
