namespace DocumentWorker.Service.Handlers;

public interface IPreviewGenerationHandler
{
    Task HandleAsync(string message, CancellationToken ct = default);
}