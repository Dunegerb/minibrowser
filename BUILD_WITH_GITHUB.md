# Build do MiniBrowser pelo GitHub Actions

Você **não precisa instalar Visual Studio nem o .NET SDK** no seu computador para gerar o browser.
O GitHub usa um runner Windows, restaura o CefSharp, publica o projeto e gera um artefato chamado `MiniBrowser-win-x64`.

## Primeira vez

1. Crie um repositório vazio no GitHub.
2. Envie **todo o conteúdo desta pasta**, incluindo a pasta oculta `.github`.
3. Use a branch `main`.
4. Ao fazer o primeiro commit/push em `main`, o workflow **Build MiniBrowser Windows x64** inicia automaticamente.

Também é possível iniciar manualmente:

1. Abra o repositório no GitHub.
2. Entre em **Actions**.
3. Selecione **Build MiniBrowser Windows x64**.
4. Clique em **Run workflow**.
5. Quando a execução terminar com sucesso, abra a execução.
6. Em **Artifacts**, baixe **MiniBrowser-win-x64**.
7. Extraia o ZIP baixado e execute `MiniBrowser.exe`.

## O que o workflow produz

O publish é:

- `Release`
- Windows x64 (`win-x64`)
- **self-contained**: o PC que executa não precisa ter .NET 8 instalado
- `PublishSingleFile=false`: o `MiniBrowser.exe` permanece junto dos binários e recursos nativos exigidos pelo CEF/Chromium

O artefato contém `MiniBrowser.exe`, CefSharp/CEF, recursos Chromium e `SHA256SUMS.txt`.

## Por que não empacotar tudo em um único EXE agora?

CEF é um runtime nativo multiprocesso. CefSharp suporta cenários de publicação single-file, mas isso exige configuração adicional para self-host do BrowserSubprocess e extração das bibliotecas nativas. Para a primeira versão, o pacote self-contained em pasta é mais simples de auditar e menos propenso a falhas de inicialização.

## Espaço

O código-fonte continua pequeno no seu computador. A compilação e os pacotes NuGet ficam no runner temporário do GitHub. O workflow mantém o artefato por **7 dias**; depois disso o GitHub o remove automaticamente.

Ao baixar, você só precisa manter a pasta final do MiniBrowser. Como ela inclui Chromium/CEF, ela ainda terá tamanho considerável — isso é inevitável para o engine, mas você evita manter SDK, Visual Studio, cache NuGet e arquivos intermediários de build localmente.

## Segurança do workflow

O workflow usa apenas actions oficiais do GitHub para checkout, instalação do .NET e upload do artefato. Ele também define `DOTNET_CLI_TELEMETRY_OPTOUT=1` durante a build.

A build não envia dados do MiniBrowser para serviços de analytics do projeto. O próprio GitHub Actions, naturalmente, executa a compilação na infraestrutura do GitHub e mantém os logs da execução conforme as políticas/configurações da conta/repositório.

## V0.4 runtime restore fix

The workflow restores the `win-x64` runtime explicitly with `SelfContained=true` before publishing with `--no-restore`. A root `global.json` pins SDK selection to .NET 8 (rolling within .NET 8 feature bands), preventing GitHub-hosted runners from silently selecting a newer installed SDK such as .NET 10.
