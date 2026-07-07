using Nop.Plugin.Widgets.AiRecommendations.Services;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Widgets.AiRecommendations.Tasks;

/// <summary>
/// Runs nightly to generate/refresh product embeddings.
/// Only re-embeds products whose content has changed since last run.
/// </summary>
public class GenerateEmbeddingsTask : IScheduleTask
{
    private readonly EmbeddingService _embeddingService;

    public GenerateEmbeddingsTask(EmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public async Task ExecuteAsync()
    {
        await _embeddingService.GenerateAllEmbeddingsAsync();
    }
}
