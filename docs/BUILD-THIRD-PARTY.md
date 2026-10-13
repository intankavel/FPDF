# Fontes e reconstrução dos mecanismos

O portátil carrega mecanismos independentes em processos locais. Nenhum código do PDFtk ou do Ghostscript foi copiado para os assemblies do FPDF. Seus textos originais de licença acompanham `licencas`, e os arquivos de fontes acompanham `fontes-terceiros`. O manifesto fornece endereços de origem, versões e hashes dos arquivos incluídos.

## PDFtk Server 2.02

`pdftk.exe` e `libiconv2.dll` foram extraídos do instalador oficial `pdftk_server-2.02-win-setup.exe`, sem instalar o produto no Windows. Não são incluídos PDFtk Free, PDFtk Pro, sua interface gráfica nem seus instaladores no ZIP final.

Descompacte `pdftk-2.02-src.zip`. A distribuição inclui o código C++, a versão modificada do iText, Bouncy Castle, os suplementos Java, os textos de licença e os Makefiles. Para a compilação nativa Windows, consulte `pdftk/Makefile.Windows.345`; ajuste `TOOLPATH` e os caminhos do `libgcj` para o ambiente de desenvolvimento. Em um ambiente MinGW/MSYS com gcc/g++/gcj 3.4.5 e libiconv, execute no diretório `pdftk`:

```sh
make -f Makefile.Windows.345
```

O binário oficial identifica gcc/gcj 3.4.5 (MinGW special). São fornecidos os arquivos publicados pelo projeto MinGW `gcc-core`, `gcc-java` e `gcc-g++` da versão 3.4.5-20060117-1, com seus patches, runtime libgcj/libstdc++ e o script `gcc-3.4.5-build.sh`. Extraia os três tar.gz para a mesma árvore, conforme o empacotamento original do MinGW; consulte o script para a configuração do compilador. As fontes de libiconv 1.9.2 são a distribuição GnuWin32, com os arquivos de compilação Windows. Seu DLL é fornecido separadamente para permitir substituição por uma versão recompilada.

As instruções e fontes originais são preservadas. A recompilação completa deste mecanismo histórico não foi realizada nesta entrega; não foi verificada equivalência binária bit a bit. Os componentes são entregues com suas licenças originais e sem alterações. As versões do MinGW runtime e w32api usadas historicamente têm termos descritos nos avisos do PDFtk; esses avisos acompanham o pacote.

## Ghostscript 10.08.0 — edição AGPL

O runtime foi copiado da instalação oficial AGPL 10.08.0 existente no computador de desenvolvimento. Inclui os programas console, DLL, bibliotecas de inicialização, fontes/recursos, perfis ICC e documentação. O desinstalador e o programa de interface Windows não são necessários à execução do FPDF e não são incluídos.

`ghostscript-10.08.0.tar.xz` é o arquivo oficial de fontes da mesma versão, com bibliotecas de terceiros e documentação de compilação. Seu SHA-512 foi comparado ao `SHA512SUMS` publicado junto à versão oficial. Extraia-o com uma ferramenta que suporte tar.xz, como 7-Zip. Para Windows, siga `doc/src/Make.rst` e os comentários de `psi/msvc.mak`, usando o ambiente de compilação C++ do Visual Studio:

```bat
nmake -f psi\msvc.mak
```

A árvore oficial documenta parâmetros de arquitetura e localização das ferramentas. Após recompilar, substitua o console/DLL em `tools/ghostscript/bin`, mantendo a árvore de recursos. No pacote, os mecanismos são localizados pela pasta do executável; o Ghostscript recebe explicitamente os caminhos de recursos via `GS_LIB` e `GS_FONTPATH`, sem depender de instalação ou registro do Windows.

## FPDF e .NET

## OCR portátil

Execute `python scripts/prepare-ocr-vendor.py` para obter a base de pacotes MSYS2, o Tesseract e suas dependências, com arquivos completos de fontes, receitas PKGBUILD e patches. O manifesto registra versões, URLs e SHA-256, conferindo os binários contra o catálogo oficial. Para reconstruir uma biblioteca, extraia seu `.src.tar.zst` e use o PKGBUILD correspondente no ambiente MSYS2 MinGW64, conforme as instruções do MSYS2. As DLLs em `tools/tesseract` podem ser substituídas por versões recompiladas compatíveis. A recompilação bit a bit não foi realizada.

Use Python 3.14 x64 para baixar as wheels exatas com `python -m pip download -r scripts/ocr-requirements.txt --only-binary=:all: --dest vendor/ocr/wheels`. Execute `python scripts/prepare-ocr-python.py` para obter as distribuições de fontes oficiais dessas mesmas versões. Copie a distribuição embutida oficial `python-3.14.7-embed-amd64.zip` para `vendor/ocr`, e as fontes `Python-3.14.7.tar.xz` para `vendor/ocr/python-sources`. Os modelos `por.traineddata` e `eng.traineddata` do tessdata_fast ficam em `vendor/ocr/models`, com a licença Apache-2.0 em `vendor/ocr/tessdata-LICENSE.txt`.

`scripts/package-ocr.py` extrai o Python embutido e as wheels, configura caminhos relativos em `python314._pth` e copia os binários nativos e os modelos. Não copia o ambiente virtual de desenvolvimento, seus caminhos absolutos ou instaladores. O manifesto do runtime registra hashes dos arquivos efetivamente distribuídos. O pacote roda sem Python/Tesseract instalados no Windows. Para reproduzir versões exatas, use os nomes e hashes dos manifestos arquivados, pois o catálogo MSYS2 pode mudar.

O código do aplicativo acompanha `codigo-fonte`, incluindo o ícone fornecido pelo autor do projeto. Abra `FPDF.sln` ou siga o README. O .NET é publicado com o programa, e seus avisos acompanham `licencas`. Para reproduzir o pacote, copie os arquivos de `fontes-terceiros` para `codigo-fonte/vendor/downloads`. Obtenha o instalador oficial do PDFtk indicado no manifesto e extraia-o com innoextract 1.9 (https://constexpr.org/innoextract/), usando `innoextract -d vendor/server-official pdftk_server-2.02-win-setup.exe`. O innoextract é uma ferramenta de preparação e não é distribuído no ZIP final.

Execute `python scripts/collect-notices.py` para preparar `vendor/notices`. Configure a origem do runtime Ghostscript AGPL 10.08.0 pelo parâmetro `-GhostscriptRoot` e execute `pwsh -File scripts/package.ps1`. Os binários são conferidos pelos hashes antes do empacotamento. Os textos de licença não são substituídos por este documento.

As fontes dos componentes de terceiros desta versão estão no arquivo FPDF-0.9.0-fontes-terceiros.zip, disponível junto ao portátil em https://github.com/intankavel/FPDF/releases/tag/v0.9.0 . O ZIP do aplicativo mantém o código-fonte do FPDF e todos os avisos de licença. Para obter as fontes de bibliotecas e mecanismos, baixe também o ZIP de fontes e extraia-o ao lado de FPDF.exe; ele cria a pasta fontes-terceiros. Preserve o acesso às fontes correspondentes ao redistribuir.
