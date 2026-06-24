# CodeWalker.Core (vendorizado)

Esta pasta é uma cópia **vendorizada** (in-tree) do `CodeWalker.Core` —
a biblioteca que parseia os formatos de recurso RSC7 da Rockstar (`.yft`, `.ytd`,
`.ydr`, etc.). O CarForge usa isso pra ler geometria e textura dos modelos de
veículo no viewer 3D.

Foi trazido pra dentro do repo de propósito: assim o projeto compila para
qualquer um que clonar, **sem precisar baixar o CodeWalker à parte** e sem
caminho absoluto hardcoded.

## Procedência

- **Upstream:** https://github.com/dexyfex/CodeWalker
- **Commit:** `485d56bec00262ed7fa472261cce7bbc6202b96e` (2025-04-11)
- **Subprojeto copiado:** `CodeWalker.Core/` (apenas ele)

## O que foi alterado em relação ao upstream

- **Removidos** `Utils/Fbx.cs` e `Utils/FbxConverter.cs` — o exportador FBX é
  derivado do FbxWriter (licença **GPL**) e o CarForge não exporta FBX. Tirar
  esses dois arquivos evita arrastar a obrigação GPL pra dentro do projeto.
  Nenhum outro arquivo do Core depende deles.
- Nada mais foi modificado. O `.csproj` segue mirando `netstandard2.0` e só
  depende do **SharpDX** (restaurado via NuGet).

## Licença / atribuição

O aviso de copyright original do upstream está preservado em
`UPSTREAM_NOTICE.txt`. O CodeWalker reúne contribuições sob termos estilo MIT
(Neodymium, Cameron Berry) somadas ao copyright de dexyfex. Mantenha o
`UPSTREAM_NOTICE.txt` junto desta pasta em qualquer redistribuição.

## Como atualizar

Para sincronizar com uma versão mais nova do upstream: recopie o conteúdo de
`CodeWalker.Core/` por cima desta pasta, **torne a remover** os dois arquivos
`Fbx*.cs`, e rode `dotnet build` na solução. Atualize o commit acima.
