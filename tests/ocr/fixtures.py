"""Synthetic, nonpersonal documents for OCR integration checks."""
import sys
from pathlib import Path
import img2pdf
import pikepdf
from PIL import Image, ImageDraw, ImageFont
from fpdf import FPDF

root = Path(sys.argv[1])
root.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 70)
lines = ["Universidade Federal do Espírito Santo", "Documento para protocolo 123456", "Reconhecimento de texto em português"]
for number, size in enumerate([(2480, 3508), (1800, 2400)], 1):
    image = Image.new("RGB", size, "white")
    draw = ImageDraw.Draw(image)
    for index, line in enumerate(lines):
        draw.text((150, 300 + index * 140), line, fill="black", font=font)
    if number == 2:
        image = image.rotate(90, expand=True)
    image.save(root / f"scan-{number}.png", dpi=(300, 300))
    (root / f"scan-{number}.pdf").write_bytes(img2pdf.convert(str(root / f"scan-{number}.png")))
image = Image.new("RGB", (1200, 1600), "white")
image.save(root / "blank.png", dpi=(300, 300))
(root / "blank.pdf").write_bytes(img2pdf.convert(str(root / "blank.png")))
text = FPDF()
text.add_page()
text.set_font("Helvetica", size=18)
text.cell(text="EXISTING-TEXT-98765")
text.output(root / "text.pdf")
with pikepdf.Pdf.new() as output:
    for name in ["scan-1.pdf", "scan-2.pdf", "text.pdf", "blank.pdf"]:
        with pikepdf.open(root / name) as source:
            output.pages.extend(source.pages)
    output.pages[1].Rotate = 90
    # Nonzero origin and cropped page exercise alignment, not just A4 scans.
    output.pages[0].CropBox = pikepdf.Array([12, 18, 580, 820])
    output.save(root / "mixed.pdf")
    output.save(root / "protected.pdf", encryption=pikepdf.Encryption(owner="owner", user="secret", R=6))
