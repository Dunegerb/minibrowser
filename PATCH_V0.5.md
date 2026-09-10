# MiniBrowser V0.5 — aplicar sobre o repositório atual

Este patch substitui somente os arquivos alterados desde a V0.4 e adiciona os novos componentes de armazenamento/histórico/DPI.

## PowerShell

Execute na raiz do seu repositório MiniBrowser:

```powershell
cd "C:\Users\Usuário\Downloads\MiniBrowser-v0.2-github-actions-source\MiniBrowser"

$patch = "$env:USERPROFILE\Downloads\MiniBrowser-v0.5-optimization-patch.zip"
$temp = Join-Path $env:TEMP "MiniBrowser-v05-patch"

Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -Path $patch -DestinationPath $temp -Force
robocopy $temp "." /E

git status
git add .
git commit -m "Optimize storage rendering UI and package size"
git push
```

`robocopy` usa códigos de saída próprios; valores baixos diferentes de zero podem significar apenas que arquivos foram copiados.

Depois do push, abra **GitHub → Actions → Build MiniBrowser Windows x64**. O workflow gera:

- `MiniBrowser-portable-x64`: não exige .NET instalado localmente.
- `MiniBrowser-thin-x64`: menor, mas exige .NET 8 Desktop Runtime.

O log da Action mostra o tamanho extraído real e os maiores arquivos de cada build.
