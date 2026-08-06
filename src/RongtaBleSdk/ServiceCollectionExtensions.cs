using Microsoft.Extensions.DependencyInjection;

namespace RongtaBleSdk;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra o RongtaBlePrinter na DI. Requer que services.AddBluetoothLE() (Shiny.BluetoothLE)
    /// já tenha sido chamado antes.
    /// </summary>
    public static IServiceCollection AddRongtaBlePrinter(this IServiceCollection services)
        => services.AddSingleton<RongtaBlePrinter>();
}
