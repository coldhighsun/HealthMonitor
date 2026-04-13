using HealthMonitor.Abstractions;
using HealthMonitor.Configuration;
using HealthMonitor.Detection;
using HealthMonitor.Monitors;
using HealthMonitor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HealthMonitor.Extensions;

/// <summary>
/// Extension methods for registering health monitors with <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a named health monitor to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">A unique logical name for this monitor.</param>
    /// <param name="configure">Optional callback to customize <see cref="HealthMonitorOptions"/>.</param>
    /// <example>
    /// <code>
    /// services.AddHealthMonitor("quotes", opt =>
    /// {
    ///     opt.DegradedThreshold = TimeSpan.FromSeconds(5);
    ///     opt.CheckInterval     = TimeSpan.FromSeconds(1);
    /// });
    /// </code>
    /// Then inject <see cref="IHealthMonitor"/> and call <c>monitor.Signal()</c> on every incoming quote.
    /// </example>
    public static IServiceCollection AddHealthMonitor(
        this IServiceCollection services,
        string name,
        Action<HealthMonitorOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Monitor name must not be null or whitespace.", nameof(name));

        // Register shared infrastructure once
        services.TryAddSingleton<ISystemTimeProvider, SystemTimeProvider>();
        services.TryAddSingleton<HealthMonitorCoordinator>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HealthMonitorHostedService>());

        // Each call contributes one registration
        var options = new HealthMonitorOptions { Name = name };
        configure?.Invoke(options);

        services.AddSingleton(new HealthMonitorRegistration(options));

#if NET8_0_OR_GREATER
        // Keyed resolution by name — .NET 8+ only
        services.AddKeyedSingleton<IHealthMonitor>(name, (sp, _) =>
        {
            var coordinator = sp.GetRequiredService<HealthMonitorCoordinator>();
            return coordinator.Monitors.Single(m => m.Name == name);
        });
#endif

        // Unkeyed enumerable — all target frameworks
        services.AddSingleton<IHealthMonitor>(sp =>
        {
            var coordinator = sp.GetRequiredService<HealthMonitorCoordinator>();
            return coordinator.Monitors.Single(m => m.Name == name);
        });

        return services;
    }
}
