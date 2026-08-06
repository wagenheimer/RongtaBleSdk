# RongtaBleSdk

SDK **não-oficial** em .NET MAUI para imprimir etiquetas em impressoras portáteis Rongta via **Bluetooth Low Energy (BLE)**, testado e confirmado na **Rongta RPP30**.

> A Rongta não documenta publicamente os UUIDs de serviço/característica BLE da RPP30. O SDK oficial deles (Android/iOS) só documenta o fluxo via Bluetooth clássico (SPP). Este projeto nasceu de engenharia reversa feita com um dispositivo físico real, usando `Windows.Devices.Bluetooth` para enumerar o GATT da impressora e confirmar os UUIDs por tentativa e erro — não são um palpite, foram testados imprimindo uma etiqueta real.

## Por que este projeto existe

Ao integrar uma RPP30 via BLE (necessário para iOS, que não permite Bluetooth clássico em apps), não existe:
- Um manual de UUIDs BLE publicado pela Rongta;
- Um exemplo documentado de uso do `BleConfigBean`/`BleInterface` no SDK Android oficial deles (que existe no jar, mas não tem exemplo no manual).

O SDK oficial da Rongta (Android/iOS/Windows/Linux) documenta apenas Bluetooth clássico (SPP). Quem precisa de BLE puro — iOS sem SPP, apps já usando stack BLE (Shiny.BluetoothLE, Plugin.BLE, CoreBluetooth, Web Bluetooth) — fica sem referência oficial.

Este repositório documenta o que foi confirmado num dispositivo físico e oferece um SDK pronto para uso em apps .NET MAUI.

## UUIDs confirmados (RPP30, firmware "BLE-TX", nome anunciado `RPP30-XXXX`)

| Papel | UUID |
|---|---|
| Serviço (UART BLE, família CC41/HM-10/JDY) | `49535343-fe7d-4ae5-8fa9-9fafd205e455` |
| Characteristic de escrita (WRITE + WRITE_NO_RESPONSE) | `49535343-8841-43f4-a8d4-ecbe34729bb3` |
| Characteristic de notificação (status) | `49535343-1e4d-4bd9-ba61-23c647249616` |

⚠️ Podem variar por lote/revisão de firmware. O repositório inclui a [ferramenta de descoberta](tools/RongtaBleDiscovery) usada para confirmar isso — rode-a na sua impressora antes de assumir que os UUIDs acima batem com o seu aparelho.

Outros serviços também expostos pela RPP30 testada (não usados por este SDK, mas documentados para referência):

```
0000ff80-0000-1000-8000-00805f9b34fb
  0000ff82 [WRITE, WRITE_NO_RESPONSE]
  0000ff81 [NOTIFY]

0000ff00-0000-1000-8000-00805f9b34fb
  0000ff02 [WRITE, WRITE_NO_RESPONSE]
  0000ff01 [NOTIFY]
  0000ff03 [NOTIFY]

0000ff10-0000-1000-8000-00805f9b34fb
  0000ff11 [WRITE_NO_RESPONSE, NOTIFY]
  0000ff12 [WRITE_NO_RESPONSE, NOTIFY]
```

## Comando suportado: CPCL

A RPP30 aceita CPCL e ESC/POS (configurável no menu físico: `Power` + `Feed` → `Cmd Mode`). Este SDK v0.1 gera **CPCL** — confirmado imprimindo uma etiqueta de teste real via BLE.

## Instalação

```xml
<PackageReference Include="RongtaBleSdk" Version="0.1.0" />
<PackageReference Include="Shiny.BluetoothLE" Version="4.0.1" />
```

Registre no `MauiProgram.cs`:

```csharp
builder.Services.AddBluetoothLE();
builder.Services.AddRongtaBlePrinter();
```

## Uso

```csharp
public class EtiquetaService(RongtaBlePrinter printer)
{
    public async Task ImprimirPesagemAsync(string identificacao, double pesoKg)
    {
        await printer.ScanAndConnectAsync(); // procura "RPP30-XXXX"

        var etiqueta = CpclLabelBuilder
            .CreateMm(widthMm: 120, heightMm: 80)
            .AddText(30, 30, "CELMI", font: 5)
            .AddText(30, 80, $"ID: {identificacao}")
            .AddText(30, 120, $"Peso: {pesoKg:F2} kg")
            .AddQrCode(30, 160, identificacao);

        await printer.PrintAsync(etiqueta);
        await printer.DisconnectAsync();
    }
}
```

## Ferramenta de descoberta (`tools/RongtaBleDiscovery`)

Console app Windows (`Windows.Devices.Bluetooth`) que escaneia BLE, conecta na impressora e lista todos os serviços/characteristics reais com suas propriedades (WRITE/NOTIFY/READ). Use antes de confiar nos UUIDs acima com um lote diferente de RPP30, ou para adaptar este SDK a outro modelo Rongta.

```powershell
cd tools/RongtaBleDiscovery
dotnet run -c Release
```

## Limitações conhecidas / próximos passos

- Testado apenas em CPCL. TSPL/ESC/ZPL não foram testados neste projeto (a RPP30 suporta os quatro, configurável no menu físico).
- Chunking fixo de 20 bytes sem negociação de MTU — funciona, mas negociar MTU maior (`TryRequestMtuAsync`) deixaria a impressão mais rápida em Android.
- Testado apenas em uma unidade física (firmware "BLE-TX"). Contribuições confirmando/corrigindo UUIDs em outros lotes são bem-vindas — abra uma issue com o output da ferramenta de descoberta.
- iOS não foi testado ainda (só validado via Windows/Shiny.BluetoothLE deve funcionar em teoria, já que a API é abstraída, mas precisa de confirmação em dispositivo real).

## Licença

MIT — veja [LICENSE](LICENSE).
