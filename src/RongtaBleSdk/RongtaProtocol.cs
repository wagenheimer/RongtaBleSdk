namespace RongtaBleSdk;

/// <summary>
/// UUIDs and transport parameters confirmed via reverse-engineering on a physical Rongta RPP30
/// (firmware "BLE-TX", "RPP30-C860"). Not officially documented by Rongta — they may vary by
/// batch/firmware, which is why discovery in <see cref="RongtaBlePrinter"/> tries a list of known
/// UUIDs and falls back to a generic search for a characteristic with WRITE.
/// </summary>
public static class RongtaProtocol
{
    /// <summary>
    /// Known (service, write characteristic) pairs, tested in the field. The first one is the
    /// one confirmed on the RPP30; the rest are common variants seen on the same BLE UART chip
    /// family (CC41/HM-10/JDY) used by several Chinese portable-printer manufacturers.
    /// </summary>
    public static readonly (string ServiceUuid, string WriteCharacteristicUuid, string? NotifyCharacteristicUuid)[] KnownUuidPairs =
    [
        ("49535343-fe7d-4ae5-8fa9-9fafd205e455", "49535343-8841-43f4-a8d4-ecbe34729bb3", "49535343-1e4d-4bd9-ba61-23c647249616"),
        ("0000ff00-0000-1000-8000-00805f9b34fb", "0000ff02-0000-1000-8000-00805f9b34fb", "0000ff01-0000-1000-8000-00805f9b34fb"),
        ("0000ff80-0000-1000-8000-00805f9b34fb", "0000ff82-0000-1000-8000-00805f9b34fb", "0000ff81-0000-1000-8000-00805f9b34fb"),
        ("0000ff10-0000-1000-8000-00805f9b34fb", "0000ff11-0000-1000-8000-00805f9b34fb", null),
        ("6e400001-b5a3-f393-e0a9-e50e24dcca9e", "6e400002-b5a3-f393-e0a9-e50e24dcca9e", "6e400003-b5a3-f393-e0a9-e50e24dcca9e"), // Nordic UART Service
    ];

    /// <summary>Default name prefix advertised by the RPP30 ("RPP30-XXXX").</summary>
    public const string DeviceNamePrefix = "RPP30";

    /// <summary>Target MTU negotiated on connect (Android; iOS/Windows negotiate automatically).</summary>
    public const int TargetMtu = 512;

    /// <summary>Safe chunk size when no MTU negotiation is available (confirmed working).</summary>
    public const int FallbackChunkSize = 20;

    /// <summary>Maximum chunk size even with a large MTU — Chinese BLE UART modules tend to choke above this.</summary>
    public const int MaxSafeChunkSize = 180;

    public static readonly TimeSpan DefaultChunkDelay = TimeSpan.FromMilliseconds(20);

    public const int WriteRetryCount = 3;
    public static readonly TimeSpan WriteRetryDelay = TimeSpan.FromMilliseconds(80);
}
