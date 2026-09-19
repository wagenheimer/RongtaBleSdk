using Shiny.BluetoothLE;

namespace RongtaBleSdk;

/// <summary>
/// High-level BLE client for Rongta RPP30 printers (and possibly other models from the same
/// BLE UART chip family). Uses Shiny.BluetoothLE as the transport. Negotiates MTU, tries a list of
/// known UUIDs (with a generic fallback), and retries chunks on transient BLE failures.
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

    /// <summary>Currently connected peripheral, if any.</summary>
    public IPeripheral? Peripheral => _peripheral;

    public bool IsConnected => _peripheral?.Status == ConnectionState.Connected && _writeCharacteristic != null;

    /// <summary>UUID of the write service/characteristic actually found on this printer.</summary>
    public (string ServiceUuid, string CharacteristicUuid)? DetectedWriteEndpoint =>
        _writeCharacteristic is null ? null : (_writeCharacteristic.Service.Uuid, _writeCharacteristic.Uuid);

    /// <summary>
    /// Scans until it finds a device whose name starts with the given prefix
    /// (default: "RPP30") and connects to it.
    /// </summary>
    public async Task<IPeripheral> ScanAndConnectAsync(
        string namePrefix = RongtaProtocol.DeviceNamePrefix,
        TimeSpan? scanTimeout = null,
        CancellationToken cancellationToken = default)
    {
        var access = await _bleManager.RequestAccessAsync();
        if (access != Shiny.AccessState.Available)
            throw new InvalidOperationException($"Bluetooth access not available: {access}");

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
                // found — cancellation was expected
            }
            finally
            {
                _bleManager.StopScan();
            }
        }

        if (found is null)
            throw new TimeoutException($"No printer with a name starting in '{namePrefix}' found within {timeout.TotalSeconds}s.");

        await ConnectAsync(found, cancellationToken);
        return found;
    }

    /// <summary>
    /// Connects to an already-discovered peripheral (e.g. via your own scanning UI), negotiates MTU,
    /// and discovers the write characteristic by trying <see cref="RongtaProtocol.KnownUuidPairs"/>
    /// and, as a last resort, any characteristic with WRITE/WRITE_NO_RESPONSE.
    /// </summary>
    public async Task ConnectAsync(IPeripheral peripheral, CancellationToken cancellationToken = default)
    {
        await peripheral.ConnectAsync(cancelToken: cancellationToken);
        _peripheral = peripheral;

        // Negotiate a larger MTU (only has a real effect on Android; iOS/Windows negotiate on their own).
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
                // UUID doesn't exist on this device — try the next candidate.
            }
        }

        // Generic fallback: first characteristic with WRITE in any service.
        var all = await peripheral.GetAllCharacteristicsAsync(cancellationToken);
        var generic = all.FirstOrDefault(c => c.CanWrite());
        if (generic is null)
            throw new InvalidOperationException(
                "No characteristic with WRITE/WRITE_NO_RESPONSE found on this printer. " +
                "Run the discovery tool (tools/RongtaBleDiscovery) to inspect the device's real GATT table.");

        SetWriteCharacteristic(generic);
    }

    void SetWriteCharacteristic(BleCharacteristicInfo characteristic)
    {
        _writeCharacteristic = characteristic;
        _writeWithResponse = !characteristic.CanWriteWithoutResponse();
    }

    /// <summary>Sends a command (CPCL or raw bytes) in chunks, retrying on transient BLE failures.</summary>
    public async Task SendAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        if (_peripheral is null || _writeCharacteristic is null || !IsConnected)
            throw new InvalidOperationException("Printer not connected. Call ConnectAsync/ScanAndConnectAsync first.");

        for (var offset = 0; offset < payload.Length; offset += _chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_peripheral.Status != ConnectionState.Connected)
                throw new IOException($"Printer disconnected during send (offset={offset}/{payload.Length}).");

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
            $"Failed to send chunk over BLE after {RongtaProtocol.WriteRetryCount} attempts (offset={offset}).",
            lastError);
    }

    /// <summary>Builds and sends a CPCL label.</summary>
    public Task PrintAsync(CpclLabelBuilder label, CancellationToken cancellationToken = default)
        => SendAsync(label.Build(), cancellationToken);

    /// <summary>Builds and sends an ESC/POS receipt (voucher, ticket, report).</summary>
    public Task PrintAsync(EscPos.EscPosReceiptBuilder receipt, CancellationToken cancellationToken = default)
        => SendAsync(receipt.Build(), cancellationToken);

    /// <summary>Sends raw ESC/POS command bytes to the printer.</summary>
    public Task PrintEscPosAsync(byte[] escPosBytes, CancellationToken cancellationToken = default)
        => SendAsync(escPosBytes, cancellationToken);

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
