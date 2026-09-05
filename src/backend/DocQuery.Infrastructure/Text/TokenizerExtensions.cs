using DocQuery.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Infrastructure.Text;

public static class TokenizerExtensions
{
    public static IServiceCollection AddTokenizer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ITokenizer, O200kTokenizer>();

        return services;
    }
}
