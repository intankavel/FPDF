# FPDF

Protótipo independente de ferramentas PDF para Windows, pensado para preparar documentos para o Lepisma. Ainda não é uma ferramenta homologada pela Ufes.

Repositório do projeto: [intankavel/FPDF](https://github.com/intankavel/FPDF). Código aberto sob AGPL-3.0-only. Os pacotes portáteis e os arquivos de terceiros são gerados separadamente; binários e documentos de teste não fazem parte do histórico Git.

## Versão 0.9.0

Novidades desta versão:

- Menu de ferramentas agrupado em Organizar páginas, Reduzir tamanho, Preparar para envio e Criar e identificar. Os títulos separam as ferramentas dentro do mesmo seletor.
- Reconhecer texto (OCR) de um PDF por vez, com português como padrão, inglês opcional, progresso e cancelamento. Usa Tesseract e OCRmyPDF localmente; os componentes estão incluídos no portátil.
- Tela de abertura com FPDF.jpg, encerrada assim que a janela principal estiver pronta, sem espera artificial.
- Compressão em lote com resumo por arquivo, mantendo os resultados concluídos se o lote for cancelado.
- Seleção de várias páginas para mover, excluir ou girar no organizador, com desfazer.
- Pré-visualização com mais detalhes nos níveis de zoom de 150% e 200%.
- **Comprimir até um limite de tamanho**: informe o máximo em MB por PDF. O aplicativo tenta 200, 150 e 96 dpi nessa ordem, sempre a partir do original, e para na primeira tentativa que atende ao limite. PDFs já dentro dele são copiados sem recompressão. Se nenhuma tentativa atender, o menor resultado disponível é salvo com aviso explícito de limite não atingido. Todas as páginas são mantidas; revise a legibilidade. Aceita um ou vários PDFs.

## Versão 0.8.1

Inclui explicações de uso e pequenos infográficos em todas as doze operações, com exemplos do que acontece com os arquivos. Os desenhos distinguem extração (um PDF) de divisão por intervalos (vários PDFs). A interface permite rolagem em janelas menores.

Novidades: portátil completo para Windows x64, com .NET, PDFtk Server e Ghostscript incluídos, e ícone próprio no executável e nas janelas.

- Juntar PDFs na ordem escolhida; arrastar arquivos e reordenar a lista.
- Dividir um PDF por tamanho máximo, com 50 MB como padrão editável.
- Extrair páginas para um novo PDF, inclusive em outra ordem.
- Dividir por intervalos: cada intervalo gera uma parte independente.
- Girar todas as páginas ou apenas as escolhidas, 90° para a direita, 90° para a esquerda ou 180°.
- Otimizar sem perda de resolução: comprimir dados internos do PDF.
- Comprimir imagens com Ghostscript em três níveis: 200, 150 ou 96 dpi.
- Mostrar tamanho antes e depois e percentual de redução.
- Salvar uma cópia exata do original se otimização ou compressão não reduzirem o tamanho.
- Comprimir e dividir em uma única operação, respeitando o tamanho máximo de cada parte.
- Converter JPG, JPEG, PNG, BMP e TIFF para PDF, incluindo TIFF multipágina e orientação EXIF das fotos.
- Aplicar marca-d'água em todas as páginas, com texto latino de até 80 caracteres, opacidade de 15%, 25% ou 40% e posição diagonal ou horizontal.
- Numerar páginas, deixando opcionalmente a capa sem número, com três formatos e seis posições no cabeçalho ou rodapé.
- Aceitar limites decimais, por exemplo 2,5 MB.
- Verificar tamanho real das partes e quantidade de páginas.
- Preservar arquivos existentes e publicar resultados somente após concluir a operação.
- Cancelar o processamento e abrir a pasta dos resultados.
- Processar localmente, sem envio de arquivos.

Cada MB corresponde a 1.000.000 bytes, uma escolha conservadora em relação a limites expressos em MiB. A divisão respeita páginas inteiras e sua ordem; não promete a quantidade mínima possível de partes. Quando uma página sozinha excede o limite, a operação falha sem salvar resultados. A divisão não reduz a resolução das imagens.

## Executar

Para distribuição, extraia `artifacts/releases/FPDF-0.9.0-win-x64.zip` e abra `FPDF.exe`. Não mova somente o executável: mantenha os arquivos de sua pasta juntos. As versões anteriores permanecem em suas pastas. A altura inicial acompanha o conteúdo da interface, dentro da área útil da tela; janelas menores continuam permitindo rolagem.

O portátil inclui .NET Desktop Runtime 9, PDFtk Server 2.02, Ghostscript AGPL 10.08.0, Python 3.14.7, OCRmyPDF 17.13.0 e Tesseract 5.5.3. O FPDF procura primeiro os mecanismos dentro de sua própria pasta. Os caminhos de recursos do Ghostscript são configurados a partir dela, permitindo mover a pasta para outro computador Windows de 64 bits. Instalações externas ainda podem ser selecionadas pela interface.

O portátil dispensa a instalação desses componentes. O botão **Verificar componentes** informa se os mecanismos locais foram encontrados. Fontes correspondentes e avisos acompanham o pacote. Valide com documentos reais e com a equipe responsável antes de adoção institucional. O executável não está assinado por um editor. Consulte `docs/LEIA-ME.txt`, `docs/THIRD-PARTY.md` e `docs/BUILD-THIRD-PARTY.md`.

O código próprio do FPDF é disponibilizado sob GNU Affero General Public License versão 3 (AGPL-3.0-only), conforme `LICENSE`. O pacote inclui seu código-fonte correspondente. Componentes externos mantêm suas próprias licenças.

## Seleção de páginas

- `1-3, 5, 8-fim`: seleciona as páginas 1 a 3, a página 5 e da página 8 até o final.
- Na extração, `5, 1-3` produz um único PDF começando pela página 5, seguida de 1, 2 e 3. Páginas repetidas são permitidas.
- Na divisão por intervalos, `1-3, 4-6, 7-fim` produz três arquivos. Páginas omitidas não são copiadas. Os intervalos podem se repetir ou se sobrepor quando isso for intencional.
- Na rotação, o campo vazio significa todas as páginas. Sobreposições são unificadas para que cada página seja girada apenas uma vez.
- Para giros diferentes, selecione **Giros por página** e use `1:E90, 2:180, 3:D90, 4-6:E90`. E90 gira 90° à esquerda; D90 gira 90° à direita; 180 inverte. O formato `1;E;90, 2;180, 3;D;90` também é aceito. Páginas não citadas ficam como estão. Sobreposições entre regras são rejeitadas para evitar giros ambíguos.
- Cada intervalo deve estar em ordem crescente, e nenhuma página pode exceder o total do documento. `fim` e `end` são aceitos para a última página.

## Compressão

Otimização sem perda não reduz a resolução das imagens. Ela pode ajudar em PDFs com dados internos descomprimidos, mas não garante redução em documentos já otimizados.

A compressão com Ghostscript regrava o PDF, recomprime imagens coloridas e em tons de cinza com níveis de qualidade JPEG diferentes, e reduz a resolução de imagens acima do nível escolhido. JPEGs e JPEG2000 já existentes passam por recompressão, mesmo quando sua resolução está abaixo do limite. Isso permite tentar reduzir imagens que antes seriam simplesmente reaproveitadas. Não aumenta a resolução de imagens pequenas.

Textos e elementos vetoriais são preservados quando possível. A redução depende do documento; 96 dpi prioriza tamanho, enquanto 200 dpi prioriza detalhes. Para imagens monocromáticas de um bit, os limites são 300, 200 e 150 dpi, respectivamente. Revise a legibilidade, links e eventuais formulários no resultado: a quantidade de páginas é verificada automaticamente, mas equivalência completa de todos os recursos do PDF não é garantida.

Quando a tentativa não diminui o PDF, o resultado é uma cópia exata do original. A interface informa o tamanho do original e da tentativa, em bytes, para explicar o resultado idêntico. PDFs predominantemente textuais, sem imagens ou já bem compactados podem não diminuir nos três níveis. Referência das configurações de imagem: https://ghostscript.readthedocs.io/en/latest/VectorDevices.html.

Os originais são preservados. Modificar PDFs assinados pode invalidar assinaturas digitais, conforme informado na interface. PDFs protegidos por senha ainda não são suportados.

## Comprimir e dividir

Adicione um PDF, escolha o limite em MB e o nível de qualidade. O FPDF comprime uma cópia temporária, divide essa cópia por páginas inteiras e verifica o tamanho de cada parte. A pasta final inclui um resumo com os tamanhos da compressão e das partes. Se uma página continuar maior que o limite, a operação não publica nenhuma parte. A resolução é reduzida apenas conforme o perfil escolhido; o FPDF não diminui a qualidade automaticamente para forçar o limite.

## Reconhecer texto (OCR)

Escolha **Criar e identificar → Reconhecer texto (OCR)**, adicione um PDF e selecione o idioma. Português é o padrão; português e inglês ou apenas inglês também estão disponíveis. O resultado é salvo com um novo nome. Reconhecimento e arquivos temporários ficam no computador. A operação adiciona uma camada de texto pesquisável, sem converter o PDF em Word e sem reduzir seu tamanho deliberadamente. As páginas são conferidas antes de salvar.

Páginas que já contêm texto são ignoradas, inclusive páginas com texto e imagens simultaneamente. Se todas já tiverem texto, o resultado é uma cópia exata do original. Confira palavras, acentos e números; digitalizações ruins ou texto deitado podem prejudicar o reconhecimento. Corrija a orientação em Girar páginas antes do OCR. O resumo distingue páginas já pesquisáveis, páginas com texto reconhecido e páginas sem texto reconhecido (incluindo páginas em branco). Não encontrar texto não prova que a página esteja em branco.

PDFs protegidos não são suportados. OCRmyPDF bloqueia alterações em documentos com assinaturas digitais ou determinadas estruturas de acessibilidade que ele não pode preservar. Cancelamento ou falha não publica resultado parcial. O tamanho pode aumentar; a camada pesquisável não representa validação de conteúdo nem garantia de acessibilidade completa.

Para preparar o ambiente de desenvolvimento Windows, instale Python 3.11 ou superior e execute `scripts/setup-ocr-dev.ps1`. O script cria um ambiente isolado em `.build/ocr-runtime`, extrai o Tesseract em `.build/ocr-tesseract` e baixa os modelos de português e inglês. Esses arquivos não são adicionados ao Git. O FPDF identifica esse ambiente quando executado dentro da pasta do projeto. Consulte [a instalação oficial do OCRmyPDF](https://ocrmypdf.readthedocs.io/en/latest/installation.html#native-windows). OCRmyPDF usa MPL-2.0; Tesseract e seus modelos usam Apache-2.0. O portátil 0.9.0 inclui o runtime e as dependências de OCR, com fontes, licenças e avisos correspondentes.

Teste a integração com `dotnet run --project tests/FPDF.SmokeTests -- --ocr-only`. Os documentos sintéticos exercitam tamanhos diferentes, recorte, rotação, texto existente, página em branco, idioma, proteção, cancelamento, alinhamento e igualdade da aparência renderizada.

## Imagens para PDF

Selecione a operação antes de adicionar as imagens. Use os botões Subir e Descer para ordenar os arquivos. JPG, JPEG, PNG, BMP e TIFF são aceitos; cada quadro de um TIFF multipágina gera uma página do PDF. A4 automático escolhe retrato ou paisagem a partir da imagem, considerando a orientação EXIF. Também é possível fixar retrato ou paisagem.

As imagens são centralizadas e ajustadas proporcionalmente, sem cortes, com margens. Transparência vira fundo branco. A conversão usa JPEG com qualidade 95 e preserva as dimensões em pixels; não é uma conversão sem perdas. Fotos e digitalizações não recebem OCR nesta versão. Uma imagem de mais de 60 milhões de pixels é rejeitada para limitar o consumo de memória.

## Marca-d'água

Adicione um PDF, escreva o texto e escolha a opacidade e a posição. A marca cinza aparece sobre o conteúdo de todas as páginas, incluindo páginas giradas. Textos com acentos e símbolos latinos são aceitos; emojis e alfabetos fora de Windows-1252 ainda não são suportados. A marca é uma identificação visual, não um mecanismo de segurança contra alterações.

## Numerar páginas

Selecione um PDF e escolha entre número simples, `Página 1` e `Página 1 de 10`. A primeira página a numerar é a posição física no documento: use 2 para deixar a capa sem número. O número inicial permite começar em 1 ou continuar uma sequência anterior. No formato `Página X de Y`, Y é o último número da sequência; por exemplo, cinco páginas numeradas a partir de 10 recebem `Página 10 de 14` até `Página 14 de 14`.

Há posições à esquerda, ao centro e à direita, no cabeçalho ou rodapé. A numeração usa texto selecionável, preserva a orientação e se ajusta ao tamanho de cada página. É sobreposta ao conteúdo: revise as margens antes de utilizar o resultado. Páginas com área visível recortada (CropBox diferente do MediaBox) ainda podem ocultar os números, pois a posição é calculada a partir do MediaBox. A sequência aceita números positivos até 1.000.000. A operação não remove uma numeração que já exista.

A aplicação cria uma camada para cada página e a combina usando a operação `multistamp`, descrita no [manual oficial do PDFtk](https://www.pdflabs.com/docs/pdftk-man-page/). Os originais e os arquivos existentes são preservados.

## Pré-visualização e organização

Selecione **Visualizar e organizar páginas**, adicione um PDF e abra a janela. Ghostscript renderiza as páginas localmente em imagens temporárias, usando a área visível do PDF; a renderização não modifica o documento. As imagens têm resolução limitada para conter consumo de memória, e não substituem a revisão no leitor de PDF final. Há zoom, carregamento de miniaturas em lotes de 20 e limite de 5.000 páginas na organização visual.

Arraste uma miniatura sobre outra para movê-la àquela posição. Também há botões para mover a página selecionada para a posição anterior ou seguinte. Excluir remove apenas do resultado. Desfazer recupera a alteração anterior; Restaurar original recupera todas as páginas, incluindo excluídas, e também pode ser desfeito. Salvar novo PDF gera o documento na ordem atual, verifica o total e preserva o original. As imagens temporárias são removidas ao fechar normalmente; encerramentos forçados podem deixar cópias na pasta temporária do Windows.

## Campos de assinatura

**Verificar assinaturas** inspeciona um PDF sem editá-lo. A organização visual e as operações de edição também inspecionam os campos. O PDFtk informa campos do tipo Signature; tanto vazios quanto preenchidos geram aviso. Esta implementação detecta campos, não valida certificados, conteúdo assinado, revogação ou validade jurídica. Não identificar campos não comprova ausência de assinatura. Falhas na inspeção são informadas como inconclusivas. PDFs protegidos por senha continuam sem suporte.

## Desenvolver

Abra `FPDF.sln` no Visual Studio Community 2022 com a carga de trabalho desktop .NET. O projeto usa .NET 9 e o SDK está fixado em `global.json`. Não existem pacotes externos nesta etapa. `NuGet.Config` desativa fontes remotas para permitir compilação local.

```powershell
dotnet build FPDF.sln
dotnet run --project tests/FPDF.SmokeTests
dotnet publish src/FPDF.App -c Release -o artifacts/app-v0.8.1 --no-self-contained
# Prepare vendor conforme docs/BUILD-THIRD-PARTY.md. A primeira execução baixa runtimes oficiais:
pwsh -File scripts/package.ps1
```

Os testes usam documentos sintéticos sem informações pessoais e verificam junção, ordem das páginas, divisão, limites, seleção de páginas, orientação, erros, cancelamento e preservação dos originais. Também validam os três níveis de compressão usando imagem de alta resolução e texto selecionável. O projeto `FPDF.UiSmoke` inicializa a interface e gera pré-visualizações de cada operação para revisão de layout.

## Próximas etapas

Validação criptográfica de assinaturas, assinatura de código do executável e validação institucional para distribuição mais ampla.

O REA não é necessário para estas operações documentadas. Poderá ser usado se uma investigação específica de comportamento exigir engenharia reversa.

As fontes dos componentes de terceiros desta versão estão no arquivo FPDF-0.9.0-fontes-terceiros.zip, disponível junto ao portátil em https://github.com/intankavel/FPDF/releases/tag/v0.9.0 . O ZIP do aplicativo mantém o código-fonte do FPDF e todos os avisos de licença. Para obter as fontes de bibliotecas e mecanismos, baixe também o ZIP de fontes e extraia-o ao lado de FPDF.exe; ele cria a pasta fontes-terceiros. Preserve o acesso às fontes correspondentes ao redistribuir.
