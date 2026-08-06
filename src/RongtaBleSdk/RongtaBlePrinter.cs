using Shiny.BluetoothLE;

namespace RongtaBleSdk;

/// <summary>
/// Cliente BLE de alto nível para impressoras Rongta RPP30 (e possivelmente outros modelos da mesma
/// família de chip UART BLE). Usa Shiny.BluetoothLE como transporte. Negocia MTU, tenta uma lista de
/// UUIDs conhecidos (com fallback genérico) e reenvia blocos com retry em caso de falha transitória.
/// </summary>
public sealed class RongtaBlePrinter : IAsyncDisposable
{
    readonly IBleManager _bleManager;
    IPeripheral? _peripheral;
    BleCharacteristicInfo? _writeCharacteristic;
    int _chunkSize = RongtaProtocol.FallbackChunkSize;
    bool _writeWithResponse = true;

    public RongtaBlePrinter(IBleManager bleManager)
    {
        _bleManager = bleManager;
    }

    /// <summary>Peripheral atualmente conectado, se houver.</summary>
    public IPeripheral? Peripheral => _peripheral;

    public bool IsConnected => _peripheral?.Status == ConnectionState.Connected && _writeCharacteristic != null;

    /// <summary>UUID do serviço/characteristic de escrita efetivamente encontrado nesta impressora.</summary>
    public (string ServiceUuid, string CharacteristicUuid)? DetectedWriteEndpoint =>
        _writeCharacteristic is null ? null : (_writeCharacteristic.Service.Uuid, _writeCharacteristic.Uuid);

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

    /// <summary>
    /// Conecta a um peripheral já descoberto (ex.: via scan próprio da UI), negocia MTU e descobre
    /// a characteristic de escrita tentando <see cref="RongtaProtocol.KnownUuidPairs"/> e, em último
    /// caso, qualquer characteristic com propriedade WRITE/WRITE_NO_RESPONSE.
    /// </summary>
    public async Task ConnectAsync(IPeripheral peripheral, CancellationToken cancellationToken = default)
    {
        await peripheral.ConnectAsync(cancelToken: cancellationToken);
        _peripheral = peripheral;

        // Negocia MTU maior (efeito real só em Android; iOS/Windows negociam sozinhos).
        var mtu = await peripheral.TryRequestMtuAsync(RongtaProtocol.TargetMtu);
        _chunkSize = mtu > 23
            ? Math.Min(mtu - 3, RongtaProtocol.MaxSafeChunkSize)
            : RongtaProtocol.FallbackChunkSize;

        await DiscoverWriteCharacteristicAsync(peripheral, cancellationToken);
    }

    async Task DiscoverWriteCharacteristicAsync(IPeripheral peripheral, CancellationToken cancellationToken)
    {
        foreach (var (serviceUuid, writeUuid, _) in RongtaProtocol.KnownUuidPairs)
        {
            try
            {
                var characteristic = await peripheral.GetCharacteristicAsync(serviceUuid, writeUuid, cancellationToken);
                if (characteristic is not null && characteristic.CanWrite())
                {
                    SetWriteCharacteristic(characteristic);
                    return;
                }
            }
            catch
            {
                // UUID não existe nesse aparelho — tenta o próximo candidato.
            }
        }

        // Fallback genérico: primeira characteristic com WRITE em qualquer serviço.
        var all = await peripheral.GetAllCharacteristicsAsync(cancellationToken);
        var generic = all.FirstOrDefault(c => c.CanWrite());
        if (generic is null)
            throw new InvalidOperationException(
                "Nenhuma characteristic com WRITE/WRITE_NO_RESPONSE encontrada nesta impressora. " +
                "Rode a ferramenta de descoberta (tools/RongtaBleDiscovery) para investigar o GATT real do aparelho.");

        SetWriteCharacteristic(generic);
    }

    void SetWriteCharacteristic(BleCharacteristicInfo characteristic)
    {
        _writeCharacteristic = characteristic;
        _writeWithResponse = !characteristic.CanWriteWithoutResponse();
    }

    /// <summary>Envia um comando (CPCL ou bytes crus) em blocos, com retry em falhas transitórias de BLE.</summary>
    public async Task SendAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        if (_peripheral is null || _writeCharacteristic is null || !IsConnected)
            throw new InvalidOperationException("Impressora não conectada. Chame ConnectAsync/ScanAndConnectAsync primeiro.");

        for (var offset = 0; offset < payload.Length; offset += _chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_peripheral.Status != ConnectionState.Connected)
                throw new IOException($"Impressora desconectada durante o envio (offset={offset}/{payload.Length}).");

            var length = Math.Min(_chunkSize, payload.Length - offset);
            var chunk = new byte[length];
            Array.Copy(payload, offset, chunk, 0, length);

            await WriteChunkWithRetryAsync(chunk, offset, cancellationToken);
            await Task.Delay(RongtaProtocol.DefaultChunkDelay, cancellationToken);
        }
    }

    async Task WriteChunkWithRetryAsync(byte[] chunk, int offset, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= RongtaProtocol.WriteRetryCount; attempt++)
        {
            try
            {
                await _peripheral!.WriteCharacteristicAsync(_writeCharacteristic!, chunk, withResponse: _writeWithResponse, cancelToken: cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (attempt < RongtaProtocol.WriteRetryCount)
                    await Task.Delay(RongtaProtocol.WriteRetryDelay, cancellationToken);
            }
        }

        throw new IOException(
            $"Falha ao enviar bloco via BLE após {RongtaProtocol.WriteRetryCount} tentativas (offset={offset}).",
            lastError);
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
        _writeCharacteristic = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_peripheral is not null)
            await DisconnectAsync();
    }
}
