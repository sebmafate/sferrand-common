using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace SFerrand.Common.Extensions;

/// <summary>
/// Helpers for <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Moves the JSON configuration sources (<c>appsettings*.json</c>) after all the other sources,
    /// so that they win over environment variables, user secrets or command-line arguments registered earlier.
    /// Handy to let a developer override an app setting from a local JSON file.
    /// </summary>
    /// <remarks>
    /// Sources are matched by exact type (<see cref="JsonConfigurationSource"/>) and keep their relative order.
    /// Call it after every source has been added: sources added afterwards still take precedence.
    /// </remarks>
    public static IConfigurationBuilder PrioritizeAppSettings(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var jsonSources = builder.Sources.Where(s => s.GetType() == typeof(JsonConfigurationSource)).ToList();
        foreach (var source in jsonSources)
        {
            builder.Sources.Remove(source);
            builder.Sources.Add(source);
        }

        return builder;
    }
}
