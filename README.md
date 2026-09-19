<div align="center">

# 🖨️ RongtaBleSdk

**Unofficial .NET MAUI SDK for printing labels over Bluetooth Low Energy on the Rongta RPP30**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-Android%20%7C%20iOS%20%7C%20MacCatalyst-blue)](https://learn.microsoft.com/dotnet/maui/)
[![Status](https://img.shields.io/badge/status-confirmed%20on%20real%20hardware-brightgreen)](#-confirmed-for-real-not-a-guess)
[![NuGet](https://img.shields.io/nuget/v/RongtaBleSdk.svg)](https://www.nuget.org/packages/RongtaBleSdk)

There is no official BLE UUID documentation for Rongta printers anywhere.
This repo exists because someone needed to print a label on an RPP30 over BLE — and had to reverse-engineer it the hard way.

🇺🇸 English (you are here) · 🇧🇷 [Ler em Português](README.pt-BR.md)

[Why](#-why-this-project-exists) ·
[Confirmed UUIDs](#-confirmed-uuids-rpp30) ·
[Install](#-installation) ·
[Usage](#-usage) ·
[Discovery tool](#-discovery-tool) ·
[Contributing](#-contributing)

</div>

---

## 📖 Why this project exists

The **Rongta RPP30** is a portable thermal label printer with "dual mode" Bluetooth (classic + BLE). If you want to print on it from:

- **iOS** → Apple doesn't allow classic Bluetooth (SPP) in third-party apps. BLE is the only option.
- **An app already built on a BLE stack** (Shiny.BluetoothLE, Plugin.BLE, CoreBluetooth, Web Bluetooth, ESP32...) → without depending on Rongta's native SDK.

...you hit a wall: **Rongta does not publish the BLE service/characteristic UUIDs anywhere**. Their official SDK (Android/iOS/Windows/Linux) only documents the classic Bluetooth flow (`BluetoothEdrConfigBean`, standard SPP UUID `00001101-...`). The BLE classes (`BleDriver`, `BleConfigBean`) exist inside the `.jar` and use the open-source [FastBLE](https://github.com/Jasonchenlijian/FastBLE) library under the hood — but there is no documented example anywhere in the official manual.

So we connected to a real RPP30, enumerated its actual GATT table, and confirmed everything by printing a real label. This repo is the result.

## ✅ Confirmed for real (not a guess)

Unlike "probable" UUIDs copied from forum threads, the values below were **validated on physical hardware**:

1. Scanned via `Windows.Devices.Bluetooth.Advertisement` → device `RPP30-C860` found.
2. Connected and enumerated every real service/characteristic over GATT (`GetGattServicesAsync` + `GetCharacteristicsAsync`).
3. Sent a test CPCL command, chunked into 20-byte blocks, through the write characteristic.
4. **The printer printed the label.** ✔️

## 🔌 Confirmed UUIDs (RPP30)

| Role | UUID |
|---|---|
| **Service** (BLE UART — CC41/HM-10/JDY chip family) | `49535343-fe7d-4ae5-8fa9-9fafd205e455` |
| **Write characteristic** (`WRITE` + `WRITE_NO_RESPONSE`) | `49535343-8841-43f4-a8d4-ecbe34729bb3` |
| **Notify characteristic** (status) | `49535343-1e4d-4bd9-ba61-23c647249616` |

> ⚠️ **These may vary by batch/firmware revision.** Before blindly trusting these values on your unit, run the [discovery tool](#-discovery-tool) included in this repo — it takes under a minute.

<details>
<summary><strong>Other services exposed by the tested RPP30 (not used by this SDK, documented for completeness)</strong></summary>

```
0000ff80-0000-1000-8000-00805f9b34fb
  0000ff82  [WRITE, WRITE_NO_RESPONSE]
  0000ff81  [NOTIFY]

0000ff00-0000-1000-8000-00805f9b34fb
  0000ff02  [WRITE, WRITE_NO_RESPONSE]
  0000ff01  [NOTIFY]
  0000ff03  [NOTIFY]

0000ff10-0000-1000-8000-00805f9b34fb
  0000ff11  [WRITE_NO_RESPONSE, NOTIFY]
  0000ff12  [WRITE_NO_RESPONSE, NOTIFY]
```

Likely variations/duplicates of the same serial transport, exposed for compatibility with different apps. Not investigated further.

</details>

## 🆕 What's new in 0.2.0

Field-tested improvements ported from a production CPCL/BLE printing pipeline (a livestock-weighing MAUI app that prints thousands of labels a day):

- **MTU negotiation** — requests a 512-byte MTU on connect (Android), auto-sizing the write chunk instead of a fixed 20 bytes. Falls back safely when negotiation isn't supported (iOS/Windows negotiate on their own).
- **Write retries** — each chunk gets up to 3 attempts with backoff before the send fails, and a disconnect mid-print now throws a clear `IOException` instead of hanging.
- **Multi-UUID discovery** — `RongtaBlePrinter` now tries a list of known UUID pairs (the confirmed RPP30 one plus a few common UART-BLE variants seen on similar Chinese printer chips) and falls back to "first writable characteristic" if none match — more likely to work out of the box on a different batch or a different Rongta model.
- **Auto label height** — `CpclLabelBuilder.CreateAutoHeightMm(width, ...)` calculates the label height from the content you add (text/barcode/QR/image Y positions) instead of requiring you to know it up front.
- **Image printing** — `AddImage(...)` converts any PNG/JPG (via SkiaSharp) to the CPCL `EG` monochrome command, so you can print logos or graphics, not just text/barcodes/QR.
- **Diacritics stripping** — accented characters are automatically stripped before sending, since CPCL on these printers is effectively ASCII-only.

## 🧾 Print command language: CPCL

The RPP30 accepts both **CPCL** and **ESC/POS** (switchable from the physical menu: hold `Power` + `Feed` → `Cmd Mode: CPCL/ESC`). This SDK generates **CPCL** — confirmed by printing a real label over BLE. TSPL and ZPL have not been tested (see [Known limitations](#-known-limitations--next-steps)).

## 📦 Installation

```powershell
dotnet add package RongtaBleSdk
```

or in your `.csproj`:

```xml
<PackageReference Include="RongtaBleSdk" Version="0.2.1" />
<PackageReference Include="Shiny.BluetoothLE" Version="4.0.1" />
<PackageReference Include="SkiaSharp" Version="3.119.4" />
```

`Shiny.BluetoothLE` and `SkiaSharp` come in transitively, but pinning your own versions avoids surprises across MAUI upgrades.

Register in `MauiProgram.cs`:

```csharp
var builder = MauiApp.CreateBuilder();

builder.Services.AddBluetoothLE();      // BLE transport (Shiny.BluetoothLE)
builder.Services.AddRongtaBlePrinter(); // RongtaBlePrinter as a singleton
```

## 🚀 Usage

```csharp
public class LabelService(RongtaBlePrinter printer)
{
    public async Task PrintWeighingAsync(string identifier, double weightKg)
    {
        // Scans until it finds an "RPP30-XXXX" device and connects
        await printer.ScanAndConnectAsync();

        var label = CpclLabelBuilder
            .CreateMm(widthMm: 120, heightMm: 80)
            .AddText(30, 30, "CELMI", font: 5)
            .AddText(30, 80, $"ID: {identifier}")
            .AddText(30, 120, $"Weight: {weightKg:F2} kg")
            .AddQrCode(30, 160, identifier);

        await printer.PrintAsync(label);
        await printer.DisconnectAsync();
    }
}
```

### Connecting to a peripheral already scanned by your own UI

```csharp
// if you already have your own scan/pairing screen (e.g. reusing IBleManager directly)
await printer.ConnectAsync(chosenPeripheral);
await printer.PrintAsync(label);
```

### Sending raw bytes (e.g. a different command dialect)

```csharp
byte[] rawCommand = Encoding.ASCII.GetBytes("! 0 200 200 210 1\r\nTEXT 4 0 30 30 HELLO\r\nFORM\r\nPRINT\r\n");
await printer.SendAsync(rawCommand);
```

## 🧩 `CpclLabelBuilder` API

| Method | Description |
|---|---|
| `CreateMm(width, height, qty, gapMm)` | Creates a fixed-height label from a size in millimeters (203dpi → 8 dots/mm) |
| `CreateDots(width, height, qty, gapDots)` | Same as above, but the size is already in dots |
| `CreateAutoHeightMm(width, qty, gapMm)` | Creates a label whose **height is calculated automatically** from whatever content you add (text/barcode/QR/image Y positions) — no need to know it up front |
| `CreateAutoHeightDots(width, qty, gapDots)` | Same as above, width already in dots |
| `AddText(x, y, text, font, size)` | Adds a line of text |
| `AddCenterText(y, text, font, size)` | Centers text horizontally across the label width |
| `AddBarcode(x, y, data, type, height, ...)` | Adds a 1D barcode (CODE128, EAN13, etc.) |
| `AddQrCode(x, y, data, cellSize)` | Adds a QR code |
| `AddLine(x, y, length, thickness)` | Line/separator |
| `AddBox(x, y, width, height, thickness)` | Rectangular bounding box |
| `AddInverse(x, y, width, height)` | White-on-black inverted highlight block |
| `AddImage(x, y, imageBytes, maxWidthDots, maxHeightDots)` | Converts a PNG/JPG (via SkiaSharp) to the CPCL `EG` command — logos, graphics, anything bitmap |
| `AddRawCommand(cpclBlock)` | Appends one or more raw CPCL command lines for anything not covered by the fluent API; still parsed for automatic height tracking |
| `Build()` | Generates the final CPCL bytes (header, `TONE`/`SETMAG`, diacritics stripped, `FORM`/`PRINT`) ready for `SendAsync` |

## 🧾 `EscPosReceiptBuilder` API (Thermal Receipts & Tickets)

For printing tickets, receipts, and reports on continuous roll paper using ESC/POS:

```csharp
var receipt = EscPosReceiptBuilder.Create58mm()
    .Initialize()
    .AlignCenter()
    .SetDoubleSize(true)
    .AddLine("CELMI PESAGEM")
    .SetDoubleSize(false)
    .AddLine("Comprovante de Pesagem")
    .AddDivider()
    .AlignLeft()
    .AddKeyValue("DATA:", "19/09/2026")
    .AddKeyValue("HORA:", "15:30")
    .AddKeyValue("PESO TOTAL:", "12.450 kg")
    .AddDivider()
    .Feed(2)
    .Cut();

await printer.PrintAsync(receipt);
```

`RongtaBlePrinter` also exposes `DetectedWriteEndpoint` after connecting, so you can log/inspect which service/characteristic pair actually worked on your unit.

## 🔍 Discovery tool

A Windows console app (`Windows.Devices.Bluetooth`) that scans BLE, connects to the printer and lists **every** real service/characteristic with its properties (`WRITE` / `WRITE_NO_RESPONSE` / `NOTIFY` / `READ`) — the exact process used to confirm the UUIDs in this README. Run it if:

- Your RPP30 is a different batch/firmware and the UUIDs above didn't match;
- You want to adapt this SDK to another Rongta model.

```powershell
cd tools/RongtaBleDiscovery
dotnet run -c Release
```

It scans for 15s, connects to the first device whose name contains `RPP`/`RONGTA`/`Printer` (or lets you type the address manually) and prints the full GATT tree to the console — optionally also sending a CPCL test label to validate the write characteristic it found.

## 🗺️ Architecture

```
┌─────────────────────────┐
│   Your MAUI App            │
│   (Android / iOS / Mac)   │
└────────────┬───────────┘
             │ DI: RongtaBlePrinter
             ▼
┌─────────────────────────┐
│   RongtaBleSdk             │
│  ┌───────────────────┐  │
│  │ CpclLabelBuilder    │  │   text, barcode, QR → CPCL bytes
│  └───────────────────┘  │
│  ┌───────────────────┐  │
│  │ RongtaBlePrinter    │  │   scan, connect, chunk & write
│  └─────────┬─────────┘  │
└────────────┼───────────┘
             │ IBleManager / IPeripheral
             ▼
┌─────────────────────────┐
│   Shiny.BluetoothLE        │   abstracts Android/iOS/Windows
└────────────┬───────────┘
             │ GATT write (service 49535343-fe7d-...)
             ▼
┌─────────────────────────┐
│   Rongta RPP30 (BLE)       │
└─────────────────────────┘
```

## ⚠️ Known limitations / next steps

- ✅ Tested printing in **CPCL**. ❌ TSPL/ESC/ZPL not tested in this project (the RPP30 supports all four, switchable from the physical menu).
- Tested on **a single physical unit** only (device name `RPP30-C860`). Contributions confirming/correcting UUIDs on other batches are welcome.
- **iOS**: the API is abstracted by Shiny.BluetoothLE and the package builds and ships for `net10.0-ios`/`net10.0-maccatalyst`, but the BLE flow itself hasn't been validated on a real iPhone/Mac yet.
- Diacritics are stripped and CPCL is sent as ASCII — good enough for Latin-script labels, but the printer's own codepage settings (`CP850`/`CP1252`/etc) aren't configurable from the SDK yet.

## 📦 Releasing

Publishing to NuGet.org uses [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) — no API key is stored anywhere. `.github/workflows/publish.yml` requests a short-lived OIDC token from GitHub Actions, exchanges it for a temporary NuGet API key, and pushes the package. It runs on `workflow_dispatch` or on pushing a `v*` tag.

## 🤝 Contributing

Tested on another Rongta model or a different RPP30 batch? Please open an issue with:

1. The full output of the [discovery tool](#-discovery-tool) run against your unit;
2. The exact device name advertised (`RPP30-XXXX`);
3. Whether the UUIDs in this README matched or not.

PRs for TSPL/ESC/ZPL support, MTU negotiation, or iOS testing are very welcome.

## 📄 License

MIT — see [LICENSE](LICENSE).

---

<div align="center">

Built by reverse-engineering what Rongta never documented, for anyone else who also needs to print on an RPP30 over BLE.

</div>
