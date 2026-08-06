using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

Console.WriteLine("=== Rongta RPP30 - BLE Discovery Tool ===");
Console.WriteLine("Escaneando dispositivos BLE por 15 segundos... Ligue a impressora e deixe perto do PC.");
Console.WriteLine();

var found = new Dictionary<ulong, string>();
var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };

watcher.Received += (w, args) =>
{
    var name = args.Advertisement.LocalName;
    if (string.IsNullOrWhiteSpace(name))
        return;

    if (!found.ContainsKey(args.BluetoothAddress))
    {
        found[args.BluetoothAddress] = name;
        Console.WriteLine($"[ENCONTRADO] {name}  |  Endereço: {args.BluetoothAddress:X}  |  RSSI: {args.RawSignalStrengthInDBm}");
    }
};

watcher.Start();
await Task.Delay(TimeSpan.FromSeconds(15));
watcher.Stop();

Console.WriteLine();
Console.WriteLine($"Total de dispositivos com nome anunciado: {found.Count}");

var candidate = found.FirstOrDefault(kv =>
    kv.Value.Contains("RPP", StringComparison.OrdinalIgnoreCase) ||
    kv.Value.Contains("RONGTA", StringComparison.OrdinalIgnoreCase) ||
    kv.Value.Contains("Printer", StringComparison.OrdinalIgnoreCase));

if (candidate.Value == null)
{
    Console.WriteLine();
    Console.WriteLine("Nenhum dispositivo com nome RPP/RONGTA/Printer encontrado automaticamente.");
    Console.WriteLine("Dispositivos encontrados:");
    foreach (var kv in found)
        Console.WriteLine($"  {kv.Value} ({kv.Key:X})");
    Console.WriteLine();
    Console.Write("Digite o endereço (hex, ex: A1B2C3D4E5F6) do dispositivo a conectar (ou ENTER para sair): ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
        return;
    candidate = new KeyValuePair<ulong, string>(Convert.ToUInt64(input, 16), "manual");
}

Console.WriteLine();
Console.WriteLine($"Conectando em {candidate.Value} ({candidate.Key:X})...");

using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(candidate.Key);
if (device is null)
{
    Console.WriteLine("Falha ao conectar (device null). Verifique se a impressora está ligada e no modo de pareamento.");
    return;
}

Console.WriteLine($"Conectado: {device.Name} | Status: {device.ConnectionStatus}");
Console.WriteLine();

var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
if (servicesResult.Status != GattCommunicationStatus.Success)
{
    Console.WriteLine($"Erro ao obter serviços GATT: {servicesResult.Status}");
    return;
}

Console.WriteLine($"=== {servicesResult.Services.Count} serviço(s) GATT encontrado(s) ===");
Console.WriteLine();

foreach (var service in servicesResult.Services)
{
    Console.WriteLine($"SERVICE: {service.Uuid}");

    var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
    if (charsResult.Status != GattCommunicationStatus.Success)
    {
        Console.WriteLine($"  (erro ao ler characteristics: {charsResult.Status})");
        continue;
    }

    foreach (var characteristic in charsResult.Characteristics)
    {
        var props = characteristic.CharacteristicProperties;
        var propsList = new List<string>();
        if (props.HasFlag(GattCharacteristicProperties.Write)) propsList.Add("WRITE");
        if (props.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)) propsList.Add("WRITE_NO_RESPONSE");
        if (props.HasFlag(GattCharacteristicProperties.Notify)) propsList.Add("NOTIFY");
        if (props.HasFlag(GattCharacteristicProperties.Indicate)) propsList.Add("INDICATE");
        if (props.HasFlag(GattCharacteristicProperties.Read)) propsList.Add("READ");

        Console.WriteLine($"  CHARACTERISTIC: {characteristic.Uuid}  [{string.Join(", ", propsList)}]");
    }

    Console.WriteLine();
}

Console.WriteLine("=== Copie o output acima para usarmos os UUIDs reais no SDK ===");
Console.WriteLine();

var targetService = servicesResult.Services.FirstOrDefault(s =>
    s.Uuid == Guid.Parse("49535343-fe7d-4ae5-8fa9-9fafd205e455"));

if (targetService is null)
{
    Console.WriteLine("Serviço 49535343-fe7d-4ae5-8fa9-9fafd205e455 não encontrado. Pulando teste de impressão.");
    Console.ReadLine();
    return;
}

var chars = await targetService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
var writeChar = chars.Characteristics.FirstOrDefault(c =>
    c.Uuid == Guid.Parse("49535343-8841-43f4-a8d4-ecbe34729bb3"));

if (writeChar is null)
{
    Console.WriteLine("Characteristic de escrita não encontrada. Pulando teste de impressão.");
    Console.ReadLine();
    return;
}

Console.Write("Deseja tentar imprimir uma etiqueta de teste CPCL agora? (s/n): ");
var answer = Console.ReadLine();
if (!string.Equals(answer, "s", StringComparison.OrdinalIgnoreCase))
{
    Console.ReadLine();
    return;
}

const string cpcl =
    "! 0 200 200 210 1\r\n" +
    "TEXT 4 0 30 30 TESTE CELMI\r\n" +
    "TEXT 4 0 30 70 RPP30 conectado via BLE\r\n" +
    "FORM\r\n" +
    "PRINT\r\n";

var payload = Encoding.ASCII.GetBytes(cpcl);
Console.WriteLine($"Enviando {payload.Length} bytes em blocos de 20...");

for (int offset = 0; offset < payload.Length; offset += 20)
{
    var chunkLen = Math.Min(20, payload.Length - offset);
    var chunk = new byte[chunkLen];
    Array.Copy(payload, offset, chunk, 0, chunkLen);

    using var writer = new Windows.Storage.Streams.DataWriter();
    writer.WriteBytes(chunk);
    var buffer = writer.DetachBuffer();

    var writeResult = await writeChar.WriteValueWithResultAsync(buffer, GattWriteOption.WriteWithoutResponse);
    Console.WriteLine($"  bloco @{offset}: {writeResult.Status}");
    await Task.Delay(30);
}

Console.WriteLine();
Console.WriteLine("Comando enviado. Verifique se a RPP30 imprimiu a etiqueta de teste.");
Console.WriteLine("Pressione ENTER para sair.");
Console.ReadLine();
