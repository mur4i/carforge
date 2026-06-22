# Viewer 3D — Fase B (carro real via CodeWalker)

A Fase A já está pronta: o viewport HelixToolkit renderiza nos quadros A/B do
dedup (hoje mostra o placeholder). Para mostrar o `.yft` real, ative o CodeWalker.

O código já está escrito e **protegido pela flag `CODEWALKER`** — enquanto a flag
não existe, nada disso compila e o app segue normal no placeholder.

## Passo a passo

### 1. Clonar o CodeWalker (ao lado do CarForge)

```powershell
cd C:\Users\Rodrigo\Desktop
git clone https://github.com/dexyfex/CodeWalker
```

Sem git? Baixe o ZIP em github.com/dexyfex/CodeWalker → Code → Download ZIP,
e extraia em `C:\Users\Rodrigo\Desktop\CodeWalker`.

### 2. Referenciar o CodeWalker.Core na solução

```powershell
cd C:\Users\Rodrigo\Desktop\CarForge
dotnet sln add ..\CodeWalker\CodeWalker.Core\CodeWalker.Core.csproj
dotnet add src\CarForge.App\CarForge.App.csproj reference ..\CodeWalker\CodeWalker.Core\CodeWalker.Core.csproj
```

### 3. Ligar a flag CODEWALKER no CarForge.App

No `src\CarForge.App\CarForge.App.csproj`, dentro do primeiro `<PropertyGroup>`:

```xml
<DefineConstants>CODEWALKER</DefineConstants>
```

### 4. Buildar

```powershell
dotnet run --project src/CarForge.App
```

Agora os quadros A/B mostram o carro real em vez da caixa.

## Se der erro de build (esperado)

O CodeWalker.Core pode ter nomes de propriedade diferentes da versão que escrevi
em `src/CarForge.App/Rendering/CodeWalkerModelLoader.cs`. Pontos prováveis:

- `drawable.DrawableModels.High` → pode ser `DrawableModelsHigh` ou `AllModels`.
- `geom.VertexData.Vertices` → pode ser outro acessor de vértices.
- `geom.IndexBuffer.Indices` → pode ser `geom.Indices`.

A estrutura conceitual é constante: **Yft → Fragment → Drawable → Model →
Geometry → vértices + índices**. Cole aqui o erro de compilação que eu ajusto os
nomes pra sua versão.

## Possível obstáculo: CodeWalker.Core no .NET 8

O CodeWalker mira .NET Framework/DirectX. Se o `CodeWalker.Core.csproj` não
buildar no .NET 8 direto, as saídas são: (a) usar uma branch/fork com suporte a
.NET, ou (b) compilar o CodeWalker.Core como DLL e referenciar o binário. A gente
decide isso quando aparecer o erro real.
