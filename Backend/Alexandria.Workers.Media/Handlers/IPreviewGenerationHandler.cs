namespace Alexandria.Workers.Media.Handlers;

public interface IPreviewGenerationHandler
{
    Task HandleAsync(string message, CancellationToken ct = default);
}