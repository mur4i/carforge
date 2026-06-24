# Viewer 3D — render do `.yft`

O viewport HelixToolkit renderiza os modelos nos quadros A/B do dedup. O render
do `.yft` real é feito pelo **CodeWalker.Core**, que agora está **vendorizado**
em `third_party/CodeWalker.Core` — não precisa baixar nada à parte.

A flag `CODEWALKER` (em `src/CarForge.App/CarForge.App.csproj`) fica **ligada** e
o loader real ([Rendering/CodeWalkerModelLoader.cs](src/CarForge.App/Rendering/CodeWalkerModelLoader.cs))
compila contra o Core vendorizado.

## Como funciona

```powershell
dotnet build CarForge.sln
dotnet run --project src/CarForge.App
```

Os quadros A/B mostram o carro real. O loader resolve geometria a partir de
`Fragment → Drawable → Model → Geometry` (vértices + índices) e a textura
diffuse do `ShaderGroup` embutido, dos `.ytd` irmãos ou do `vehshare.ytd`.
Decodifica BC1–5 via CodeWalker (`DDSIO`) e BC7 via `BCnEncoder`. Um log de
diagnóstico por modelo vai pra `%TEMP%\carforge_viewer.log`.

## Voltar pro placeholder (sem CodeWalker)

Se quiser compilar a GUI sem o render real, remova `CODEWALKER` de
`<DefineConstants>` no `CarForge.App.csproj`. O `CodeWalkerModelLoader.cs` inteiro
está protegido por `#if CODEWALKER`, então o app cai no placeholder 3D e a
referência ao Core vendorizado deixa de ser necessária.

## Atualizar o CodeWalker.Core vendorizado

Ver [third_party/CodeWalker.Core/README.md](third_party/CodeWalker.Core/README.md)
para procedência (commit de origem), o que foi removido (exportador FBX/GPL) e o
passo a passo de sincronizar com uma versão mais nova do upstream.
