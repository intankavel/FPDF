"""OCRmyPDF plugin for plain, local page progress."""
import sys
from ocrmypdf import hookimpl


class Progress:
    def __init__(self, *, total=0, desc="", **kwargs):
        self.total = total
        self.desc = desc
        self.completed = 0
        self.last_page = 0

    def __enter__(self):
        if self.desc == "OCR":
            self.show_page(1)
        return self

    def __exit__(self, *args):
        return False

    def update(self, amount=1, **kwargs):
        self.completed += amount
        if self.desc == "OCR":
            page = min(int(self.completed) + 1, int(self.total))
            self.show_page(page)

    def show_page(self, page):
        if page != self.last_page:
            self.last_page = page
            print(f"FPDF:Reconhecendo texto: página {page} de {int(self.total)}…", file=sys.stderr, flush=True)


@hookimpl
def get_progressbar_class():
    return Progress
