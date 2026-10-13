"""Local OCR bridge. Only status and aggregate counts leave this process."""
import json
import logging
import shutil
import sys
from pathlib import Path

import ocrmypdf
import pikepdf
import pypdfium2 as pdfium


def status(message):
    print("FPDF:" + message, file=sys.stderr, flush=True)


def text_pages(path):
    result = []
    with pdfium.PdfDocument(path) as doc:
        for index in range(len(doc)):
            page = doc[index]
            text = page.get_textpage()
            try:
                result.append(bool(text.get_text_range().strip()))
            finally:
                text.close()
                page.close()
    return result


class PageProgress(logging.Handler):
    failed_page = False

    def emit(self, record):
        # OCRmyPDF may skip a timed-out page; never report that as complete OCR.
        message = record.getMessage().lower()
        if any(term in message for term in ("timed out", "timeout", "took too long", "image too large")):
            self.failed_page = True


def main():
    source, output = map(Path, sys.argv[1:3])
    language = sys.argv[3]
    if language not in ("por", "por+eng", "eng"):
        raise ValueError("Idioma de OCR inválido.")
    with pikepdf.open(source) as pdf:
        if pdf.is_encrypted:
            raise ValueError("O PDF está protegido. Utilize uma cópia sem proteção para reconhecer o texto.")
    status("Verificando quais páginas já têm texto…")
    before = text_pages(source)
    total = len(before)
    if all(before):
        shutil.copyfile(source, output)
    else:
        handler = PageProgress()
        logging.getLogger().addHandler(handler)
        logging.getLogger().setLevel(logging.INFO)
        # A progress plugin reports each page without exposing recognized text.
        ocrmypdf.ocr(
            source, output, language=language.split("+"), skip_text=True,
            output_type="pdf", optimize=0, jobs=1, use_threads=True,
            progress_bar=True, plugins=[str(Path(__file__).with_name("progress.py"))],
            tesseract_timeout=180,
        )
        if handler.failed_page:
            raise ValueError("Uma página excedeu o tempo ou tamanho permitido para OCR. Nenhum resultado foi salvo.")
    status("Conferindo o texto e as páginas do resultado…")
    after = text_pages(output)
    if len(after) != total:
        raise ValueError("A quantidade de páginas mudou. Nenhum resultado foi salvo.")
    print(json.dumps({"Pages": total, "AlreadyText": sum(before),
                      "Recognized": sum(not old and new for old, new in zip(before, after)),
                      "WithoutText": sum(not page for page in after)}))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        messages = {
            "MissingDependencyError": "Falta um componente do OCR ou o modelo do idioma. Verifique os componentes.",
            "DigitalSignatureError": "Este PDF tem assinatura digital. O OCR pode invalidá-la; utilize uma cópia sem assinatura.",
            "EncryptedPdfError": "O PDF está protegido por senha. Utilize uma cópia sem proteção.",
            "TaggedPDFError": "Este PDF contém estrutura de acessibilidade que precisa ser preservada. O OCR foi interrompido.",
            "InputFileError": "Não foi possível ler o PDF. Verifique se está danificado ou protegido.",
        }
        print(messages.get(type(error).__name__, str(error)), file=sys.stderr, flush=True)
        sys.exit(1)
