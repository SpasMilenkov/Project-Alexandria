namespace DocumentWorker.Service.Handlers;

public class PreviewGenerationHandler(ILogger<PreviewGenerationHandler> logger) : IPreviewGenerationHandler
{
    public async Task HandleAsync(string message)
    {
        logger.LogInformation("Processing: {Message}", message);
        
        await Task.Delay(100); // Simulate work
    }
}