using Microsoft.Extensions.DependencyInjection;
using PawPoint.Services;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Parsing;

namespace PawPoint.API.Extensions;

public static class AssistantServiceExtensions
{
    public static IServiceCollection AddAssistantServices(this IServiceCollection services)
    {
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddScoped<IAssistantDataService, AssistantDataService>();
        services.AddScoped<IIntentParser, KeywordIntentParser>();

        return services;
    }
}