# CarForge

Ferramenta de gestão de packs de veículos para FiveM (QBCore/QBox/VRP).
Limpa, separa, analisa, gera manifest, edita handling e — na fase final —
visualiza o modelo 3D do carro pra você decidir o que é duplicado de verdade.

> Nascido da dor real: um pack `skips_veiculos` de **14 GB** com **1695 modelos**,
> **805 nomes duplicados** e **36 colisões** com a frota do server.

## Arquitetura (camadas desacopladas)

```
CarForge.Core   → motor puro (parsing de .meta, dedup, colisão, tuning,
                  manifest, split). Zero dependência de UI. Testável.
CarForge.Cli    → app de console que roda o motor JÁ (sem precisar do WPF).
                  Use hoje pra limpar o pack.
CarForge.App    → GUI WPF: módulos + dedup com 2 previews 3D lado a lado, e
                  render 3D real do .yft via CodeWalker.Core + HelixToolkit.
third_party/CodeWalker.Core → leitor RSC7 (.yft/.ytd) vendorizado in-tree.
CarForge.Core.Tests → xUnit.
```

O `Core` não conhece a UI. Isso permite: testar o motor sem tela, trocar a UI
sem reescrever lógica, e plugar o viewer 3D depois sem mexer no resto.

## Por que C#/.NET e não web?

O requisito de **renderizar o `.yft` em 3D** decide a stack. O único parser
maduro e aberto do formato RSC7 da Rockstar é o **CodeWalker.Core** (C#).
Fazer isso em web seria reimplementar anos de trabalho. Logo: .NET.

O CodeWalker.Core está **vendorizado** em `third_party/CodeWalker.Core` — não
precisa baixar nada à parte, a solução compila no clone. Procedência e licença
em [third_party/CodeWalker.Core/README.md](third_party/CodeWalker.Core/README.md).

## Build (no Windows)

Requisitos: .NET 8 SDK (e, pra GUI, Visual Studio 2022 ou `dotnet` com workload Desktop).

```powershell
cd CarForge
dotnet build

# rodar a CLI contra um pack:
dotnet run --project src/CarForge.Cli -- analyze "C:\caminho\do\skips_veiculos"

# comparar colisão com a frota existente:
dotnet run --project src/CarForge.Cli -- analyze "C:\...\skips_veiculos" --against "C:\...\resources"

# gerar manifest pra um resource:
dotnet run --project src/CarForge.Cli -- manifest "C:\...\meu_carro"

# rodar os testes do motor:
dotnet test
```

## Rodar a GUI (Windows)

```powershell
dotnet run --project src/CarForge.App
```

Na janela: clique em `Procurar…`, escolha a pasta do pack, clique em `Escanear`.
A lista mostra os conflitos de nome; selecione um pra comparar A vs B lado a lado.

### Renomear spawn em todo lugar (sem dar erro)

O botão `Renomear spawn` troca o `modelName` em TODOS os lugares de uma vez:
`vehicles.meta`, `carvariations.meta` e os arquivos de stream (`.yft`/`.ytd`,
incluindo `+hi`). Opcionalmente também `handlingId`/`handlingName` e o id do
modkit. O matching é por token exato — renomear `as350` nunca toca `as350pc`
(validado no pack real: 22 ocorrências corrigidas, 0 falsos positivos).

> Sempre faça backup antes de aplicar. O rename mexe em disco.

## Status

- [x] Motor de análise validado em pack real (Python proto → portado pra C#)
- [x] Core: scan, duplicados, colisão, tuning, manifest
- [x] Core: rename de spawn em todo lugar (com dry-run/preview)
- [x] Core: splitter (monolito → 1 resource por unidade, com manifest)
- [x] Core: editor de handling (lê/edita/salva campos)
- [x] CLI funcional (analyze, manifest, split)
- [x] GUI WPF com abas: Faxina/dedup, Split, Handling, Viewer
- [x] Viewer 3D: contrato `IVehicleModelLoader` + placeholder pronto
- [x] Viewer 3D: render real do `.yft` (HelixToolkit + CodeWalker vendorizado)

## Limitações conhecidas (honestas)

- O CodeWalker.Core está vendorizado (`third_party/`), com licença mista — o
  copyright de topo do upstream não tem concessão formal explícita. Ver
  [third_party/CodeWalker.Core/README.md](third_party/CodeWalker.Core/README.md).
- A lógica do Core foi validada em Python contra o pack real de 14 GB antes de
  ser portada pra C#.
