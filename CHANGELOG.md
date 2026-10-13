# Histórico

## 0.9.0

- Menu organizado em quatro categorias, preservando o seletor e os infográficos.
- OCR local em português e inglês, com progresso, cancelamento e resumo por tipo de página. Python, OCRmyPDF e Tesseract incluídos no portátil.
- Compressão em lote com resumo por arquivo e preservação dos resultados já concluídos em caso de cancelamento.
- Compressão até um limite editável, tentando as três qualidades a partir do original e avisando quando o limite não for atingido.
- Seleção de várias páginas para mover, excluir e girar no organizador, com desfazer.
- Maior nitidez nas pré-visualizações em zoom de 150% e 200%.
- Tela de abertura com FPDF.jpg, sem espera artificial.
- Fontes, bibliotecas e licenças correspondentes dos novos componentes acompanham o pacote.

## 0.8.1

- Janela inicial mais larga e altura ajustada automaticamente ao conteúdo, respeitando a área útil da tela.
- Verificação de ausência de rolagem vertical no tamanho natural da interface inicial.
- Preparação do repositório público, com binários e artefatos de testes excluídos do versionamento.

## 0.8

- Portátil completo para Windows x64: .NET, PDFtk Server 2.02 e Ghostscript AGPL 10.08.0 incluídos.
- Fontes de terceiros, bibliotecas do PDFtk, textos originais de licença, manifestos e instruções de reconstrução.
- Caminhos de recursos do Ghostscript definidos a partir do pacote, preservando funcionamento fora das instalações do Windows.
- Ícone FPDF.ico incorporado ao executável e às janelas.
- Verificação do pacote inclui mecanismos locais e ícone; testes podem usar exclusivamente as ferramentas do portátil.

## 0.7

- Visualização de páginas com miniaturas, ampliação, zoom e carregamento em lotes.
- Exclusão e reordenação por arraste ou botões, desfazer e restauração do original.
- Inspeção de campos de assinatura por PDFtk, com limitações explícitas e aviso antes das operações de edição.
- Assistente de dependências com links oficiais e detecção de instalações.
- Pacote Windows x64 com .NET incluído, código-fonte, AGPLv3, avisos de terceiros, instruções e hash SHA-256. PDFtk/Ghostscript continuam externos.

## 0.6

- Numeração de páginas: três formatos, seis posições, primeira página a numerar e número inicial configuráveis.
- Infográfico da numeração e opção de deixar a capa sem número.
- Verificação da sequência, preservação de conteúdo, cancelamento e posição visual em páginas giradas, A4, Carta, paisagem e com origem deslocada.

## 0.5

- Infográficos explicativos para todas as dez ferramentas, mantendo os desenhos de extração e divisão por intervalos.
- Exemplos de ordem dos arquivos, rotação, compressão, limite de tamanho, conversão de imagens e marca-d'água.

## 0.4

- Compressão seguida de divisão por tamanho, com publicação dos resultados apenas após a verificação.
- Imagens para PDF: A4 automático, retrato ou paisagem; transparência, orientação EXIF e TIFF multipágina.
- Marca-d'água em texto, com acentos, opacidade e posição configuráveis.
- Testes de orientação e fundo branco das imagens renderizadas; integridade das páginas e resultados da operação combinada.

## 0.3

- Explicações de uso e infográficos para extração e divisão.
- Giros diferentes por página, incluindo o formato com ponto e vírgula.
- Recompressão de JPEGs e informação do tamanho da tentativa quando não houver redução.

## 0.2

- Extração, divisão por intervalos, rotação, otimização e compressão com Ghostscript.

## 0.1

- Junção e divisão por tamanho máximo, com 50 MB como padrão editável.
