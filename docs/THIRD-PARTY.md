# Componentes de terceiros

O código próprio do FPDF é disponibilizado sob AGPL-3.0-only. O pacote inclui seu código-fonte correspondente e o texto integral da licença. Não atribui licença do FPDF a componentes de terceiros.

## Incluídos no pacote

O .NET Runtime e o Windows Desktop Runtime da Microsoft são publicados junto com o aplicativo. Seus textos de licença e avisos acompanham a pasta `licencas`; a versão exata está no arquivo `FPDF.runtimeconfig.json`. O empacotamento copia os avisos diretamente dos pacotes oficiais usados na compilação. As licenças próprias desses componentes continuam aplicáveis.

## Mecanismos incluídos no portátil

O OCR usa Python 3.14.7 (distribuição embutida oficial para Windows x64), OCRmyPDF 17.13.0 e Tesseract 5.5.3, como processos separados. Python e suas bibliotecas têm licenças próprias; OCRmyPDF e pikepdf usam MPL-2.0; Tesseract e os modelos tessdata_fast usam Apache-2.0. As dependências nativas do Tesseract são os pacotes oficiais MSYS2 identificados em `fontes-terceiros/ocr/msys-manifest.json`, com licenças específicas, incluindo GPL, LGPL e exceções de runtime onde aplicáveis. As DLLs são mantidas separadas e substituíveis. Não atribuímos Apache-2.0 a todas essas bibliotecas.

As fontes MSYS2 completas (incluindo receitas PKGBUILD e patches), distribuições de fontes Python e manifestos acompanham `fontes-terceiros/ocr`. Os textos originais de licença ficam em `licencas/ocr`, junto aos metadados das distribuições; os avisos incorporados também permanecem nos pacotes Python. As fontes de OCR e seus modelos/fonte Noto têm avisos próprios. As wheels do Python são usadas sem alterações de código; seus recursos e textos de licença são preservados. As versões são fixadas em `scripts/ocr-requirements.txt`.

PDFtk Server 2.02 é executado como um programa separado em `tools/pdftk.exe`, com `libiconv2.dll` substituível. Os binários foram extraídos da distribuição oficial do Server. Seu texto original oferece GPL versão 2 ou posterior. Os avisos das bibliotecas acompanham `licencas/pdftk`, com fontes do PDFtk/iText modificado/Bouncy Castle, gcc/gcj/libstdc++, libiconv e scripts publicados em `fontes-terceiros`. PDFtk Free e PDFtk Pro não são incluídos. Consulte [licença do PDFtk](https://www.pdflabs.com/docs/pdftk-license/).

Ghostscript AGPL 10.08.0 é executado como um programa separado em `tools/ghostscript`, para compressão e pré-visualização. São incluídos console, DLL, recursos, fontes, perfis ICC, bibliotecas e documentação da instalação oficial, com o arquivo oficial de fontes da mesma versão. Os textos originais de licença acompanham `licencas` e as árvores de recursos/fontes. Os avisos de bibliotecas foram coletados sem alteração dos arquivos oficiais de fontes. Consulte [downloads oficiais](https://ghostscript.com/releases/gsdnld.html) e [orientações de licença da Artifex](https://ghostscript.com/licensing/).

O manifesto identifica arquivos de origem e hashes dos binários e fontes incluídos. As instruções de reconstrução estão em `BUILD-THIRD-PARTY.md`. Fontes e avisos acompanham o pacote completo; preserve-os na redistribuição. Não foi realizada recompilação bit a bit dos mecanismos de terceiros nesta entrega. O pacote utiliza os binários oficiais sem alterações.

As fontes dos componentes de terceiros desta versão estão no arquivo FPDF-0.9.0-fontes-terceiros.zip, disponível junto ao portátil em https://github.com/intankavel/FPDF/releases/tag/v0.9.0 . O ZIP do aplicativo mantém o código-fonte do FPDF e todos os avisos de licença. Para obter as fontes de bibliotecas e mecanismos, baixe também o ZIP de fontes e extraia-o ao lado de FPDF.exe; ele cria a pasta fontes-terceiros. Preserve o acesso às fontes correspondentes ao redistribuir.
