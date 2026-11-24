using DocumentFlow.Application.Abstractions;
using DocumentFlow.Application.Auth;
using DocumentFlow.Application.Documents;
using DocumentFlow.Application.Events;
using DocumentFlow.Application.Workspace;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocumentFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddSingleton<IDomainEventHandler, AuditTrailHandler>();
        services.AddSingleton<IDomainEventHandler, NotificationHandler>();

        services.AddScoped<AuthService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<DirectoryService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AdminService>();

        return services;
    }
}
