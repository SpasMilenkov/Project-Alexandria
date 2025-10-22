namespace MediaWorkerService.Handlers;

public interface IPreviewGenerationHandler
{
    Task HandleAsync(string message, CancellationToken ct = default);
}