import sys
from pathlib import Path
import pikepdf
import pypdfium2 as pdfium
from PIL import ImageChops, ImageDraw

source, output, root = map(Path, sys.argv[1:4])
with pikepdf.open(source) as before, pikepdf.open(output) as after:
    assert len(before.pages) == len(after.pages) == 4
    for old, new in zip(before.pages, after.pages):
        assert list(old.MediaBox) == list(new.MediaBox), "Formato da página mudou"
        assert list(old.get("/CropBox", old.MediaBox)) == list(new.get("/CropBox", new.MediaBox)), "Recorte da página mudou"
        assert old.get("/Rotate", 0) == new.get("/Rotate", 0), "Orientação mudou"

with pdfium.PdfDocument(source) as before, pdfium.PdfDocument(output) as after:
    for index in range(4):
        old, new = before[index], after[index]
        text = new.get_textpage()
        recognized = text.get_text_range()
        if index < 2:
            assert "123456" in recognized and "Universidade" in recognized, recognized
            # Text coordinates must overlap the printed ink, including rotated/cropped pages.
            bitmap = new.render(scale=1)
            overlay = bitmap.to_pil().convert("RGB")
            coordinates = bitmap.get_posconv(new)
            width, height = overlay.size
            def pixel_box(char):
                left, bottom, right, top = text.get_charbox(char)
                x1, y1 = coordinates.to_bitmap(left, bottom)
                x2, y2 = coordinates.to_bitmap(right, top)
                return min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)
            for char in range(text.count_chars()):
                character = text.get_text_range(char, 1)
                if not character.isalnum():
                    continue
                left, top, right, bottom = pixel_box(char)
                assert 0 <= left <= right <= width and 0 <= top <= bottom <= height
            if index < 2:
                left, top, right, bottom = pixel_box(recognized.index("Universidade"))
                region = overlay.crop((left, top, right+1, bottom+1)).convert("L")
                assert region.getextrema()[0] < 100, "Texto invisível desalinhado da imagem"
                draw = ImageDraw.Draw(overlay)
                for char in range(text.count_chars()):
                    left, top, right, bottom = pixel_box(char)
                    if right > left and bottom > top:
                        draw.rectangle((left, top, right, bottom), outline="red", width=1)
                overlay.save(root / f"alinhamento-ocr-{index+1}.png")
        if index == 2:
            assert "EXISTING-TEXT-98765" in recognized
        image_old = old.render(scale=1).to_pil().convert("RGB")
        image_new = new.render(scale=1).to_pil().convert("RGB")
        assert image_old.size == image_new.size
        assert ImageChops.difference(image_old, image_new).getbbox() is None, "A aparência mudou"
        image_new.save(root / f"resultado-pagina-{index+1}.png")
        text.close(); old.close(); new.close()
