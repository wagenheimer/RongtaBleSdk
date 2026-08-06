<div align="center">

# 🖨️ RongtaBleSdk

**SDK .NET MAUI não-oficial para imprimir etiquetas via Bluetooth Low Energy na Rongta RPP30**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-Android%20%7C%20iOS%20%7C%20MacCatalyst-blue)](https://learn.microsoft.com/dotnet/maui/)
[![Status](https://img.shields.io/badge/status-confirmado%20em%20dispositivo%20real-brightgreen)](#-confirmado-de-verdade-não-é-palpite)
[![NuGet](https://img.shields.io/nuget/v/RongtaBleSdk.svg)](https://www.nuget.org/packages/RongtaBleSdk)

🇺🇸 [Read in English](README.md) · 🇧🇷 Português (você está aqui)

Nenhum UUID BLE oficial documentado pela Rongta existe por aí.
Este repositório existe porque alguém precisava imprimir uma etiqueta numa RPP30 via BLE — e teve que descobrir tudo na marra.

[Por quê](#-por-que-este-projeto-existe) ·
[UUIDs confirmados](#-uuids-confirmados-rpp30-firmware-ble-tx) ·
[Instalação](#-instalação) ·
[Uso](#-uso) ·
[Ferramenta de descoberta](#-ferramenta-de-descoberta) ·
[Contribuindo](#-contribuindo)

</div>

---

## 📖 Por que este projeto existe

A **Rongta RPP30** é uma impressora portátil de etiquetas térmica, com Bluetooth "dual mode" (clássico + BLE). Se você quer imprimir nela a partir de:

- **iOS** → Apple não permite Bluetooth clássico (SPP) em apps de terceiros. Só resta BLE.
- **App próprio já usando stack BLE** (Shiny.BluetoothLE, Plugin.BLE, CoreBluetooth, Web Bluetooth, ESP32...) → sem depender do SDK nativo da Rongta.

...você esbarra num buraco: **a Rongta não publica os UUIDs de serviço/característica BLE em lugar nenhum**. O SDK oficial deles (Android/iOS/Windows/Linux) documenta apenas o fluxo via Bluetooth clássico (`BluetoothEdrConfigBean`, UUID SPP padrão `00001101-...`). As classes de BLE (`BleDriver`, `BleConfigBean`) existem dentro do `.jar`, usam a lib open-source [FastBLE](https://github.com/Jasonchenlijian/FastBLE) por baixo — mas não têm nenhum exemplo documentado no manual oficial.

Então conectamos numa RPP30 física, enumeramos o GATT real dela, e confirmamos tudo imprimindo uma etiqueta de verdade. Este repositório é o resultado.

## ✅ Confirmado de verdade (não é palpite)

Diferente de UUIDs "prováveis" copiados de fóruns, os valores abaixo foram **validados numa RPP30 física**:

1. Escaneada via `Windows.Devices.Bluetooth.Advertisement` → dispositivo `RPP30-C860` encontrado.
2. Conectada e enumerados todos os serviços/characteristics reais via GATT (`GetGattServicesAsync` + `GetCharacteristicsAsync`).
3. Enviado um comando CPCL de teste, em blocos de 20 bytes, pela characteristic de escrita.
4. **A impressora imprimiu a etiqueta.** ✔️

## 🔌 UUIDs confirmados (RPP30, firmware "BLE-TX")

| Papel | UUID |
|---|---|
| **Serviço** (UART BLE — família de chip CC41/HM-10/JDY) | `49535343-fe7d-4ae5-8fa9-9fafd205e455` |
| **Characteristic de escrita** (`WRITE` + `WRITE_NO_RESPONSE`) | `49535343-8841-43f4-a8d4-ecbe34729bb3` |
| **Characteristic de notificação** (status) | `49535343-1e4d-4bd9-ba61-23c647249616` |

> ⚠️ **Podem variar por lote/revisão de firmware.** Antes de confiar cegamente nesses valores no seu aparelho, rode a [ferramenta de descoberta](#-ferramenta-de-descoberta) incluída neste repo — leva menos de 1 minuto.

<details>
<summary><strong>Outros serviços expostos pela RPP30 testada (não usados por este SDK, documentados por completude)</strong></summary>

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

Provavelmente variações/duplicatas do mesmo transporte serial, expostas por compatibilidade com apps diferentes. Não investigadas a fundo.

</details>

## 🆕 Novidades da 0.2.0

Melhorias testadas em campo, portadas de um pipeline de impressão CPCL/BLE em produção (um app MAUI de pesagem de gado que imprime milhares de etiquetas por dia):

- **Negociação de MTU** — solicita MTU de 512 bytes na conexão (Android), ajustando automaticamente o tamanho do bloco em vez de fixar 20 bytes. Cai com segurança no fallback quando a negociação não é suportada (iOS/Windows negociam sozinhos).
- **Retry de escrita** — cada bloco tem até 3 tentativas com backoff antes de falhar, e uma desconexão no meio da impressão agora lança um `IOException` claro em vez de travar.
- **Descoberta multi-UUID** — o `RongtaBlePrinter` agora tenta uma lista de pares de UUID conhecidos (o confirmado na RPP30 mais algumas variações comuns de chip UART-BLE vistas em impressoras chinesas parecidas) e cai para "primeira characteristic com escrita" se nenhum bater — mais chance de funcionar de primeira num lote diferente ou noutro modelo Rongta.
- **Altura de etiqueta automática** — `CpclLabelBuilder.CreateAutoHeightMm(largura, ...)` calcula a altura da etiqueta a partir do conteúdo adicionado (posições Y de texto/código de barras/QR/imagem), sem precisar saber de antemão.
- **Impressão de imagem** — `AddImage(...)` converte qualquer PNG/JPG (via SkiaSharp) para o comando monocromático `EG` do CPCL, permitindo imprimir logos ou gráficos, não só texto/código de barras/QR.
- **Remoção de acentos** — caracteres acentuados são removidos automaticamente antes do envio, já que o CPCL nessas impressoras é, na prática, ASCII puro.

## 🧾 Comando de impressão: CPCL

A RPP30 aceita **CPCL** e **ESC/POS** (alternável no menu físico: segure `Power` e `Feed` → `Cmd Mode: CPCL/ESC`). Este SDK gera **CPCL** — confirmado imprimindo uma etiqueta real via BLE. TSPL e ZPL não foram testados (ver [Limitações](#-limitações-conhecidas--próximos-passos)).

## 📦 Instalação

```powershell
dotnet add package RongtaBleSdk
```

ou no `.csproj`:

```xml
<PackageReference Include="RongtaBleSdk" Version="0.2.1" />
<PackageReference Include="Shiny.BluetoothLE" Version="4.0.1" />
<PackageReference Include="SkiaSharp" Version="3.119.4" />
```

`Shiny.BluetoothLE` e `SkiaSharp` vêm transitivamente, mas fixar sua própria versão evita surpresas em upgrades do MAUI.

Registre no `MauiProgram.cs`:

```csharp
var builder = MauiApp.CreateBuilder();

builder.Services.AddBluetoothLE();      // transporte BLE (Shiny.BluetoothLE)
builder.Services.AddRongtaBlePrinter(); // RongtaBlePrinter como singleton
```

## 🚀 Uso

```csharp
public class EtiquetaService(RongtaBlePrinter printer)
{
    public async Task ImprimirPesagemAsync(string identificacao, double pesoKg)
    {
        // Escaneia até achar um dispositivo "RPP30-XXXX" e conecta
        await printer.ScanAndConnectAsync();

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

### Conectando a um peripheral já escaneado pela sua própria UI

```csharp
// se você já tem sua tela de scan/pareamento (ex.: reaproveitando IBleManager diretamente)
await printer.ConnectAsync(peripheralEscolhido);
await printer.PrintAsync(etiqueta);
```

### Enviando bytes crus (ex.: outro dialeto de comando)

```csharp
byte[] comandoCru = Encoding.ASCII.GetBytes("! 0 200 200 210 1\r\nTEXT 4 0 30 30 OLA\r\nFORM\r\nPRINT\r\n");
await printer.SendAsync(comandoCru);
```

## 🧩 API do `CpclLabelBuilder`

| Método | Descrição |
|---|---|
| `CreateMm(largura, altura, qtd, gapMm)` | Cria etiqueta de altura fixa a partir do tamanho em milímetros (203dpi → 8 dots/mm) |
| `CreateDots(largura, altura, qtd, gapDots)` | Igual acima, mas o tamanho já em dots |
| `CreateAutoHeightMm(largura, qtd, gapMm)` | Cria etiqueta com **altura calculada automaticamente** a partir do conteúdo adicionado (posições Y de texto/código de barras/QR/imagem) — sem precisar saber de antemão |
| `CreateAutoHeightDots(largura, qtd, gapDots)` | Igual acima, largura já em dots |
| `AddText(x, y, texto, font, size)` | Adiciona uma linha de texto |
| `AddBarcode(x, y, dados, tipo, altura, ...)` | Adiciona código de barras 1D (CODE128, EAN13, etc.) |
| `AddQrCode(x, y, dados, cellSize)` | Adiciona QR Code |
| `AddLine(x, y, comprimento, espessura)` | Linha/separador |
| `AddImage(x, y, imageBytes, maxWidthDots, maxHeightDots)` | Converte um PNG/JPG (via SkiaSharp) para o comando CPCL `EG` — logos, gráficos, qualquer bitmap |
| `AddRawCommand(blocoCpcl)` | Anexa uma ou mais linhas de comando CPCL cru, para casos não cobertos pela API fluente; ainda entra no cálculo automático de altura |
| `Build()` | Gera os bytes CPCL finais (header, `TONE`/`SETMAG`, acentos removidos, `FORM`/`PRINT`) prontos para `SendAsync` |

O `RongtaBlePrinter` também expõe `DetectedWriteEndpoint` depois de conectar, para você logar/inspecionar qual par serviço/characteristic funcionou de fato no seu aparelho.

## 🔍 Ferramenta de descoberta

Console app Windows (`Windows.Devices.Bluetooth`) que escaneia BLE, conecta na impressora e lista **todos** os serviços/characteristics reais com suas propriedades (`WRITE` / `WRITE_NO_RESPONSE` / `NOTIFY` / `READ`) — o mesmo processo usado para confirmar os UUIDs deste README. Rode-a se:

- Sua RPP30 tem um lote/firmware diferente e os UUIDs acima não bateram;
- Você quer adaptar este SDK para outro modelo Rongta.

```powershell
cd tools/RongtaBleDiscovery
dotnet run -c Release
```

Ela escaneia por 15s, conecta no primeiro dispositivo com nome contendo `RPP`/`RONGTA`/`Printer` (ou deixa você digitar o endereço manualmente) e imprime toda a árvore GATT no console — opcionalmente também envia uma etiqueta de teste CPCL para validar a característica de escrita encontrada.

## 🗺️ Arquitetura

```
┌─────────────────────────┐
│   Seu App MAUI            │
│   (Android / iOS / Mac)   │
└────────────┬───────────┘
             │ DI: RongtaBlePrinter
             ▼
┌─────────────────────────┐
│   RongtaBleSdk             │
│  ┌───────────────────┐  │
│  │ CpclLabelBuilder    │  │   texto, barcode, QR → bytes CPCL
│  └───────────────────┘  │
│  ┌───────────────────┐  │
│  │ RongtaBlePrinter    │  │   scan, connect, chunk & write
│  └─────────┬─────────┘  │
└────────────┼───────────┘
             │ IBleManager / IPeripheral
             ▼
┌─────────────────────────┐
│   Shiny.BluetoothLE        │   abstrai Android/iOS/Windows
└────────────┬───────────┘
             │ GATT write (service 49535343-fe7d-...)
             ▼
┌─────────────────────────┐
│   Rongta RPP30 (BLE)       │
└─────────────────────────┘
```

## ⚠️ Limitações conhecidas / próximos passos

- ✅ Testado imprimindo em **CPCL**. ❌ TSPL/ESC/ZPL não testados neste projeto (a RPP30 suporta os quatro, configurável no menu físico).
- Chunking fixo de 20 bytes sem negociação de MTU — funciona, mas negociar MTU maior (`TryRequestMtuAsync`) deixaria a impressão mais rápida em Android.
- Testado apenas em **uma unidade física** (firmware "BLE-TX", nome `RPP30-C860`). Contribuições confirmando/corrigindo UUIDs em outros lotes são bem-vindas.
- **iOS**: a API é abstraída pelo Shiny.BluetoothLE e deveria funcionar em teoria, mas ainda não foi validado num iPhone real.
- Codepage fixo em ISO-8859-1 — acentuação pode variar dependendo da codepage configurada na impressora (`CP850`/`CP1252`/etc, ver menu físico).

## 📦 Publicação (release)

A publicação no NuGet.org usa [Trusted Publishing](https://learn.microsoft.com/pt-br/nuget/nuget-org/trusted-publishing) — nenhuma API key fica armazenada em lugar nenhum. O `.github/workflows/publish.yml` solicita um token OIDC de curta duração do GitHub Actions, troca por uma chave de API temporária do NuGet e publica o pacote. Roda via `workflow_dispatch` ou ao dar push numa tag `v*`.

## 🤝 Contribuindo

Testou em outro modelo Rongta ou outro lote da RPP30? Abra uma issue com:

1. O output completo da [ferramenta de descoberta](#-ferramenta-de-descoberta) rodada no seu aparelho;
2. Nome exato anunciado pelo dispositivo (`RPP30-XXXX`);
3. Se os UUIDs deste README bateram ou não.

PRs para TSPL/ESC/ZPL, negociação de MTU, ou testes em iOS são muito bem-vindos.

## 📄 Licença

MIT — veja [LICENSE](LICENSE).

---

<div align="center">

Feito descobrindo na marra o que a Rongta não documentou, para quem também precisa imprimir numa RPP30 via BLE.

</div>
