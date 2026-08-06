namespace RongtaBleSdk;

/// <summary>
/// UUIDs e parâmetros de transporte confirmados via engenharia reversa em uma Rongta RPP30 física
/// (firmware BLE-TX, "RPP30-C860"). Não são documentados oficialmente pela Rongta — podem variar
/// por lote/firmware, por isso a descoberta em <see cref="RongtaBlePrinter"/> tenta uma lista de
/// UUIDs conhecidos e cai para uma busca genérica por characteristic com WRITE.
/// </summary>
public static class RongtaProtocol
{
    /// <summary>
    /// Pares (serviço, characteristic de escrita) conhecidos, testados em campo. O primeiro é o
    /// confirmado na RPP30; os demais são variações comuns na mesma família de chip UART BLE
    /// (CC41/HM-10/JDY) usada por vários fabricantes chineses de impressora portátil.
    /// </summary>
    public static readonly (string ServiceUuid, string WriteCharacteristicUuid, string? NotifyCharacteristicUuid)[] KnownUuidPairs =
    [
        ("49535343-fe7d-4ae5-8fa9-9fafd205e455", "49535343-8841-43f4-a8d4-ecbe34729bb3", "49535343-1e4d-4bd9-ba61-23c647249616"),
        ("0000ff00-0000-1000-8000-00805f9b34fb", "0000ff02-0000-1000-8000-00805f9b34fb", "0000ff01-0000-1000-8000-00805f9b34fb"),
        ("0000ff80-0000-1000-8000-00805f9b34fb", "0000ff82-0000-1000-8000-00805f9b34fb", "0000ff81-0000-1000-8000-00805f9b34fb"),
        ("0000ff10-0000-1000-8000-00805f9b34fb", "0000ff11-0000-1000-8000-00805f9b34fb", null),
        ("6e400001-b5a3-f393-e0a9-e50e24dcca9e", "6e400002-b5a3-f393-e0a9-e50e24dcca9e", "6e400003-b5a3-f393-e0a9-e50e24dcca9e"), // Nordic UART Service
    ];

    /// <summary>Prefixo padrão do nome anunciado pela RPP30 ("RPP30-XXXX").</summary>
    public const string DeviceNamePrefix = "RPP30";

    /// <summary>MTU alvo negociado na conexão (Android; iOS/Windows negociam automaticamente).</summary>
    public const int TargetMtu = 512;

    /// <summary>Chunk seguro quando não há negociação de MTU disponível (confirmado funcionando).</summary>
    public const int FallbackChunkSize = 20;

    /// <summary>Chunk máximo mesmo com MTU grande — módulos UART BLE chineses costumam engasgar acima disso.</summary>
    public const int MaxSafeChunkSize = 180;

    public static readonly TimeSpan DefaultChunkDelay = TimeSpan.FromMilliseconds(20);

    public const int WriteRetryCount = 3;
    public static readonly TimeSpan WriteRetryDelay = TimeSpan.FromMilliseconds(80);
}
