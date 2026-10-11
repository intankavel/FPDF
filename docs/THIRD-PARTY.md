# Componentes de terceiros

O código próprio do FPDF é disponibilizado sob AGPL-3.0-only. O pacote inclui seu código-fonte correspondente e o texto integral da licença. Não atribui licença do FPDF a componentes de terceiros.

## Incluídos no pacote

O .NET Runtime e o Windows Desktop Runtime da Microsoft são publicados junto com o aplicativo. Seus textos de licença e avisos acompanham a pasta `licencas`; a versão exata está no arquivo `FPDF.runtimeconfig.json`. O empacotamento copia os avisos diretamente dos pacotes oficiais usados na compilação. As licenças próprias desses componentes continuam aplicáveis.

## Mecanismos incluídos no portátil

PDFtk Server 2.02 é executado como um programa separado em `tools/pdftk.exe`, com `libiconv2.dll` substituível. Os binários foram extraídos da distribuição oficial do Server. Seu texto original oferece GPL versão 2 ou posterior. Os avisos das bibliotecas acompanham `licencas/pdftk`, com fontes do PDFtk/iText modificado/Bouncy Castle, gcc/gcj/libstdc++, libiconv e scripts publicados em `fontes-terceiros`. PDFtk Free e PDFtk Pro não são incluídos. Consulte [licença do PDFtk](https://www.pdflabs.com/docs/pdftk-license/).

Ghostscript AGPL 10.08.0 é executado como um programa separado em `tools/ghostscript`, para compressão e pré-visualização. São incluídos console, DLL, recursos, fontes, perfis ICC, bibliotecas e documentação da instalação oficial, com o arquivo oficial de fontes da mesma versão. Os textos originais de licença acompanham `licencas` e as árvores de recursos/fontes. Os avisos de bibliotecas foram coletados sem alteração dos arquivos oficiais de fontes. Consulte [downloads oficiais](https://ghostscript.com/releases/gsdnld.html) e [orientações de licença da Artifex](https://ghostscript.com/licensing/).

O manifesto identifica arquivos de origem e hashes dos binários e fontes incluídos. As instruções de reconstrução estão em `BUILD-THIRD-PARTY.md`. Fontes e avisos acompanham o pacote completo; preserve-os na redistribuição. Não foi realizada recompilação bit a bit dos mecanismos de terceiros nesta entrega. O pacote utiliza os binários oficiais sem alterações.
