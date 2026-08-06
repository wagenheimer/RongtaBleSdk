using Microsoft.Extensions.DependencyInjection;

namespace RongtaBleSdk;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers RongtaBlePrinter in DI. Requires services.AddBluetoothLE() (Shiny.BluetoothLE)
    /// to have been called first.
    /// </summary>
    public static IServiceCollection AddRongtaBlePrinter(this IServiceCollection services)
        => services.AddSingleton<RongtaBlePrinter>();
}
