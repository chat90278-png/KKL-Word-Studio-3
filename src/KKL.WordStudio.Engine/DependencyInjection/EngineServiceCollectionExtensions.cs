namespace KKL.WordStudio.Engine.DependencyInjection;

using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Microsoft.Extensions.DependencyInjection;

public static class EngineServiceCollectionExtensions
{
    public static IServiceCollection AddWordStudioEngine(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentLayoutEngine, DeterministicDocumentLayoutEngine>();
        return services;
    }
}
