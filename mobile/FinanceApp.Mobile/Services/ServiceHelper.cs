namespace FinanceApp.Mobile.Services;

/// <summary>Acesso ao provedor de serviços do app para resolver páginas/VMs na navegação.</summary>
public static class ServiceHelper
{
    public static T Get<T>() where T : notnull
        => Current.GetService(typeof(T)) is T svc
            ? svc
            : throw new InvalidOperationException($"Serviço {typeof(T).Name} não registrado.");

    public static IServiceProvider Current =>
        IPlatformApplication.Current?.Services
        ?? throw new InvalidOperationException("Service provider indisponível.");
}
