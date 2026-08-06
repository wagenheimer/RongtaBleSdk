using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

Console.WriteLine("=== Rongta RPP30 - BLE Discovery Tool ===");
Console.WriteLine("Scanning for BLE devices for 15 seconds... Turn on the printer and keep it near the PC.");
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
        Console.WriteLine($"[FOUND] {name}  |  Address: {args.BluetoothAddress:X}  |  RSSI: {args.RawSignalStrengthInDBm}");
    }
};

watcher.Start();
await Task.Delay(TimeSpan.FromSeconds(15));
watcher.Stop();

Console.WriteLine();
Console.WriteLine($"Total devices with an advertised name: {found.Count}");

var candidate = found.FirstOrDefault(kv =>
    kv.Value.Contains("RPP", StringComparison.OrdinalIgnoreCase) ||
    kv.Value.Contains("RONGTA", StringComparison.OrdinalIgnoreCase) ||
    kv.Value.Contains("Printer", StringComparison.OrdinalIgnoreCase));

if (candidate.Value == null)
{
    Console.WriteLine();
    Console.WriteLine("No device with an RPP/RONGTA/Printer name found automatically.");
    Console.WriteLine("Devices found:");
    foreach (var kv in found)
        Console.WriteLine($"  {kv.Value} ({kv.Key:X})");
    Console.WriteLine();
    Console.Write("Type the address (hex, e.g. A1B2C3D4E5F6) of the device to connect to (or press ENTER to exit): ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
        return;
    candidate = new KeyValuePair<ulong, string>(Convert.ToUInt64(input, 16), "manual");
}

Console.WriteLine();
Console.WriteLine($"Connecting to {candidate.Value} ({candidate.Key:X})...");

using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(candidate.Key);
if (device is null)
{
    Console.WriteLine("Failed to connect (device is null). Check that the printer is turned on and in pairing mode.");
    return;
}

Console.WriteLine($"Connected: {device.Name} | Status: {device.ConnectionStatus}");
Console.WriteLine();

var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
if (servicesResult.Status != GattCommunicationStatus.Success)
{
    Console.WriteLine($"Error retrieving GATT services: {servicesResult.Status}");
    return;
}

Console.WriteLine($"=== {servicesResult.Services.Count} GATT service(s) found ===");
Console.WriteLine();

foreach (var service in servicesResult.Services)
{
    Console.WriteLine($"SERVICE: {service.Uuid}");

    var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
    if (charsResult.Status != GattCommunicationStatus.Success)
    {
        Console.WriteLine($"  (error reading characteristics: {charsResult.Status})");
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

Console.WriteLine("=== Copy the output above to use the real UUIDs in the SDK ===");
Console.WriteLine();

var targetService = servicesResult.Services.FirstOrDefault(s =>
    s.Uuid == Guid.Parse("49535343-fe7d-4ae5-8fa9-9fafd205e455"));

if (targetService is null)
{
    Console.WriteLine("Service 49535343-fe7d-4ae5-8fa9-9fafd205e455 not found. Skipping print test.");
    Console.ReadLine();
    return;
}

var chars = await targetService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
var writeChar = chars.Characteristics.FirstOrDefault(c =>
    c.Uuid == Guid.Parse("49535343-8841-43f4-a8d4-ecbe34729bb3"));

if (writeChar is null)
{
    Console.WriteLine("Write characteristic not found. Skipping print test.");
    Console.ReadLine();
    return;
}

Console.Write("Try printing a CPCL test label now? (y/n): ");
var answer = Console.ReadLine();
if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
{
    Console.ReadLine();
    return;
}

const string cpcl =
    "! 0 200 200 210 1\r\n" +
    "TEXT 4 0 30 30 RongtaBleSdk TEST\r\n" +
    "TEXT 4 0 30 70 RPP30 connected via BLE\r\n" +
    "FORM\r\n" +
    "PRINT\r\n";

var payload = Encoding.ASCII.GetBytes(cpcl);
Console.WriteLine($"Sending {payload.Length} bytes in 20-byte chunks...");

for (int offset = 0; offset < payload.Length; offset += 20)
{
    var chunkLen = Math.Min(20, payload.Length - offset);
    var chunk = new byte[chunkLen];
    Array.Copy(payload, offset, chunk, 0, chunkLen);

    using var writer = new Windows.Storage.Streams.DataWriter();
    writer.WriteBytes(chunk);
    var buffer = writer.DetachBuffer();

    var writeResult = await writeChar.WriteValueWithResultAsync(buffer, GattWriteOption.WriteWithoutResponse);
    Console.WriteLine($"  chunk @{offset}: {writeResult.Status}");
    await Task.Delay(30);
}

Console.WriteLine();
Console.WriteLine("Command sent. Check whether the RPP30 printed the test label.");
Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();
