namespace RongtaBleSdk;

/// <summary>
/// UUIDs confirmados via engenharia reversa em uma Rongta RPP30 física (firmware BLE-TX, "RPP30-C860"),
/// usando nRF/Windows.Devices.Bluetooth para enumerar o GATT real do aparelho.
/// Não são documentados oficialmente pela Rongta — podem variar por lote/firmware.
/// </summary>
public static class RongtaProtocol
{
    /// <summary>Serviço UART BLE (família de chip CC41/HM-10/JDY) usado pela RPP30.</summary>
    public const string ServiceUuid = "49535343-fe7d-4ae5-8fa9-9fafd205e455";

    /// <summary>Characteristic de escrita (WRITE + WRITE_NO_RESPONSE) — envio de comandos CPCL.</summary>
    public const string WriteCharacteristicUuid = "49535343-8841-43f4-a8d4-ecbe34729bb3";

    /// <summary>Characteristic de notificação (status/resposta da impressora).</summary>
    public const string NotifyCharacteristicUuid = "49535343-1e4d-4bd9-ba61-23c647249616";

    /// <summary>Prefixo padrão do nome anunciado pela RPP30 ("RPP30-XXXX").</summary>
    public const string DeviceNamePrefix = "RPP30";

    /// <summary>Tamanho de bloco seguro para escrita sem negociar MTU (confirmado funcionando).</summary>
    public const int DefaultChunkSize = 20;

    /// <summary>Atraso entre blocos para não sobrecarregar o buffer BLE do módulo UART.</summary>
    public static readonly TimeSpan DefaultChunkDelay = TimeSpan.FromMilliseconds(30);
}
