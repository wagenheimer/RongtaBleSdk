using Shiny.BluetoothLE;

namespace RongtaBleSdk;

/// <summary>
/// Cliente BLE de alto nível para impressoras Rongta RPP30 (e possivelmente outros modelos da mesma
/// família de chip UART BLE). Usa Shiny.BluetoothLE como transporte.
/// </summary>
public sealed class RongtaBlePrinter : IAsyncDisposable
{
    readonly IBleManager _bleManager;
    IPeripheral? _peripheral;

    public RongtaBlePrinter(IBleManager bleManager)
    {
        _bleManager = bleManager;
    }

    /// <summary>Peripheral atualmente conectado, se houver.</summary>
    public IPeripheral? Peripheral => _peripheral;

    public bool IsConnected => _peripheral?.Status == ConnectionState.Connected;

    /// <summary>
    /// Escaneia até encontrar um dispositivo cujo nome comece com o prefixo informado
    /// (padrão: "RPP30") e conecta nele.
    /// </summary>
    public async Task<IPeripheral> ScanAndConnectAsync(
        string namePrefix = RongtaProtocol.DeviceNamePrefix,
        TimeSpan? scanTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var access = await _bleManager.RequestAccessAsync();
        if (access != Shiny.AccessState.Available)
            throw new InvalidOperationException($"Acesso Bluetooth não disponível: {access}");

        var timeout = scanTimeout ?? TimeSpan.FromSeconds(15);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        IPeripheral? found = null;
        using (_bleManager.Scan().Subscribe(result =>
               {
                   var name = result.Peripheral.Name ?? result.AdvertisementData.LocalName;
                   if (name is not null && name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                   {
                       found = result.Peripheral;
                       cts.Cancel();
                   }
               }))
        {
            try
            {
                await Task.Delay(timeout, cts.Token);
            }
            catch (OperationCanceledException) when (found != null)
            {
                // encontrado — cancelamento esperado
            }
            finally
            {
                _bleManager.StopScan();
            }
        }

        if (found is null)
            throw new TimeoutException($"Nenhuma impressora com nome iniciando em '{namePrefix}' encontrada em {timeout.TotalSeconds}s.");

        await ConnectAsync(found, cancellationToken);
        return found;
    }

    /// <summary>Conecta a um peripheral já descoberto (ex.: via scan próprio da UI).</summary>
    public async Task ConnectAsync(IPeripheral peripheral, CancellationToken cancellationToken = default)
    {
        await peripheral.ConnectAsync(cancelToken: cancellationToken);
        _peripheral = peripheral;
    }

    /// <summary>Envia um comando (CPCL ou bytes crus) em blocos, no ritmo que o módulo UART BLE aceita.</summary>
    public async Task SendAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        if (_peripheral is null || !IsConnected)
            throw new InvalidOperationException("Impressora não conectada. Chame ConnectAsync/ScanAndConnectAsync primeiro.");

        for (var offset = 0; offset < payload.Length; offset += RongtaProtocol.DefaultChunkSize)
        {
            var length = Math.Min(RongtaProtocol.DefaultChunkSize, payload.Length - offset);
            var chunk = new byte[length];
            Array.Copy(payload, offset, chunk, 0, length);

            await _peripheral.WriteCharacteristicAsync(
                RongtaProtocol.ServiceUuid,
                RongtaProtocol.WriteCharacteristicUuid,
                chunk,
                withResponse: false,
                cancelToken: cancellationToken);

            await Task.Delay(RongtaProtocol.DefaultChunkDelay, cancellationToken);
        }
    }

    /// <summary>Monta e envia uma etiqueta CPCL.</summary>
    public Task PrintAsync(CpclLabelBuilder label, CancellationToken cancellationToken = default)
        => SendAsync(label.Build(), cancellationToken);

    public async Task DisconnectAsync()
    {
        if (_peripheral is null)
            return;

        await _peripheral.DisconnectAsync();
        _peripheral = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_peripheral is not null)
            await DisconnectAsync();
    }
}
