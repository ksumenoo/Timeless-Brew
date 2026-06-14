# -*- coding: utf-8 -*-
"""Хелперы для дописывания Курсовичок.docx в стиле существующего документа.
Соглашения документа (выяснены инспекцией):
  - базовый шрифт Times New Roman 14 (sz=28 half-pt), межстрочный 1.5 (line=360);
  - тело (Normal): firstLine=720 twips (1.27 см), spacing after=120 (6 pt), выравнивание влево;
  - H1 (главы): по центру, полужирный, КАПСОМ; глава начинается с новой страницы
    (пустой абзац с разрывом страницы перед заголовком);
  - H2/H3 (параграфы): слева, полужирный, с абзацным отступом firstLine=720;
  - списки: стиль 'List Paragraph' + numPr (ilvl 0, numId 2) — маркированный список документа;
  - подпись рисунка: Normal по центру, 'Рис. N — Название', spacing before=120 after=240;
  - листинги кода добавляются моноширинным Consolas 10 с лёгкой заливкой.
"""
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
from docx.shared import Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH


def _twips_firstline(p, twips=720):
    pf = p.paragraph_format
    pf.first_line_indent = Pt(twips / 20.0)


def h1(doc, text, page_break=True):
    """Заголовок главы: новая страница + центр, полужирный, капс (из стиля)."""
    if page_break:
        br = doc.add_paragraph(style='Normal')
        r = br.add_run()
        brk = OxmlElement('w:br'); brk.set(qn('w:type'), 'page')
        r._r.append(brk)
    p = doc.add_paragraph(text, style='Heading 1')
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    return p


def h2(doc, text):
    p = doc.add_paragraph(text, style='Heading 2')
    _twips_firstline(p, 720)
    return p


def h3(doc, text):
    p = doc.add_paragraph(text, style='Heading 3')
    _twips_firstline(p, 720)
    return p


def body(doc, text):
    p = doc.add_paragraph(text, style='Normal')
    _twips_firstline(p, 720)
    p.paragraph_format.space_after = Pt(6)
    return p


def bullet(doc, text):
    p = doc.add_paragraph(text, style='List Paragraph')
    pPr = p._p.get_or_add_pPr()
    numPr = OxmlElement('w:numPr')
    ilvl = OxmlElement('w:ilvl'); ilvl.set(qn('w:val'), '0')
    numId = OxmlElement('w:numId'); numId.set(qn('w:val'), '2')
    numPr.append(ilvl); numPr.append(numId)
    pPr.append(numPr)
    p.paragraph_format.space_after = Pt(4)
    return p


def figure(doc, caption, placeholder=True):
    """Место под скриншот + центрированная подпись 'Рис. N — …'."""
    if placeholder:
        ph = doc.add_paragraph('[ место для рисунка — вставьте скриншот ]', style='Normal')
        ph.alignment = WD_ALIGN_PARAGRAPH.CENTER
        ph.paragraph_format.first_line_indent = Pt(0)
        ph.paragraph_format.space_before = Pt(6)
        ph.paragraph_format.space_after = Pt(2)
        for r in ph.runs:
            r.font.color.rgb = RGBColor(0x90, 0x90, 0x90); r.font.italic = True
    cap = doc.add_paragraph(caption, style='Normal')
    cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    cap.paragraph_format.first_line_indent = Pt(0)
    cap.paragraph_format.space_before = Pt(2)
    cap.paragraph_format.space_after = Pt(12)
    for r in cap.runs:
        r.font.italic = True
    return cap


def listing_caption(doc, text):
    """Подпись листинга над кодом: 'Листинг N — …', слева, курсив."""
    p = doc.add_paragraph(text, style='Normal')
    p.paragraph_format.first_line_indent = Pt(0)
    p.paragraph_format.space_before = Pt(8)
    p.paragraph_format.space_after = Pt(2)
    for r in p.runs:
        r.font.italic = True
    return p


def code(doc, code_str):
    """Блок кода: каждая строка — абзац Consolas 10, одинарный интервал, лёгкая заливка."""
    lines = code_str.split('\n')
    n = len(lines)
    for i, line in enumerate(lines):
        p = doc.add_paragraph(style='Normal')
        pf = p.paragraph_format
        pf.first_line_indent = Pt(0)
        pf.left_indent = Pt(8)
        pf.space_after = Pt(0)
        pf.space_before = Pt(0)
        pf.line_spacing = 1.0
        # лёгкая заливка фона строки кода
        pPr = p._p.get_or_add_pPr()
        shd = OxmlElement('w:shd')
        shd.set(qn('w:val'), 'clear'); shd.set(qn('w:color'), 'auto'); shd.set(qn('w:fill'), 'F4F4F4')
        pPr.append(shd)
        run = p.add_run(line if line != '' else ' ')
        run.font.name = 'Consolas'
        run.font.size = Pt(10)
        # шрифт для всех диапазонов (ascii/hAnsi/cs/eastAsia)
        rPr = run._r.get_or_add_rPr()
        rFonts = rPr.find(qn('w:rFonts'))
        if rFonts is None:
            rFonts = OxmlElement('w:rFonts'); rPr.insert(0, rFonts)
        for a in ('w:ascii', 'w:hAnsi', 'w:cs', 'w:eastAsia'):
            rFonts.set(qn(a), 'Consolas')
    return n


def blank(doc):
    return doc.add_paragraph('', style='Normal')


def table_caption(doc, text):
    """Подпись таблицы: 'Таблица N — …', слева, полужирно (по требованиям)."""
    p = doc.add_paragraph(text, style='Normal')
    p.paragraph_format.first_line_indent = Pt(0)
    p.paragraph_format.space_before = Pt(10)
    p.paragraph_format.space_after = Pt(2)
    for r in p.runs:
        r.font.bold = True
    return p


def table(doc, rows, widths_cm=None, total_cm=15.5, header=True, font_pt=12):
    """Таблица с полными одинарными границами (как существующие таблицы документа).
    rows — список списков строк; первая строка — шапка (полужирная)."""
    from docx.shared import Cm
    n_cols = len(rows[0])
    t = doc.add_table(rows=len(rows), cols=n_cols)
    t.autofit = False
    tblPr = t._tbl.tblPr
    borders = OxmlElement('w:tblBorders')
    for edge in ('top', 'left', 'bottom', 'right', 'insideH', 'insideV'):
        e = OxmlElement('w:' + edge)
        e.set(qn('w:val'), 'single'); e.set(qn('w:sz'), '4'); e.set(qn('w:space'), '0'); e.set(qn('w:color'), 'auto')
        borders.append(e)
    tblPr.append(borders)
    if widths_cm is None:
        widths_cm = [total_cm / n_cols] * n_cols
    for ri, row in enumerate(rows):
        for ci, val in enumerate(row):
            cell = t.cell(ri, ci)
            cell.width = Cm(widths_cm[ci])
            p = cell.paragraphs[0]
            p.paragraph_format.space_after = Pt(0)
            p.paragraph_format.space_before = Pt(0)
            p.paragraph_format.line_spacing = 1.0
            run = p.add_run(str(val))
            run.font.size = Pt(font_pt)
            if header and ri == 0:
                run.font.bold = True
    # запас по интервалу после таблицы
    doc.add_paragraph('', style='Normal').paragraph_format.space_after = Pt(6)
    return t
