#!/usr/bin/env python3
"""C01-01 original source and requirements matrix coverage. Read-only, stdlib only.
Does not compile the SaaS, connect to VPS, change databases or certify C01.
"""
from collections import Counter
from hashlib import sha256
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / 'docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx'
MATRIX = ROOT / 'docs/c01/C01_01_MATRIZ_REQUISITOS.md'
DECISIONS = ROOT / 'docs/c01/C01_01_DEPENDENCIAS_Y_GATE.md'
MASTER_REFERENCE = ROOT / 'docs/MASTER_REFERENCE.md'
W = '{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
EXPECTED_SHA = 'e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e'


def check(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def text_of(element: ET.Element) -> str:
    return ''.join(node.text or '' for node in element.iter(W + 't'))


def para_style(element: ET.Element) -> str:
    ppr = element.find(W + 'pPr')
    ps = ppr.find(W + 'pStyle') if ppr is not None else None
    return ps.get(W + 'val') if ps is not None else 'Normal'


def source_parts() -> tuple[list[tuple[str, str]], list[list[list[str]]], str]:
    source = WORD.read_bytes()
    digest = sha256(source).hexdigest()
    check(canonical_active_word(EXPECTED_SHA), 'Word changed: old coverage is invalid')
    check(EXPECTED_SHA in MASTER_REFERENCE.read_text(encoding='utf-8'),
          'Canonical master reference does not contain Word hash')
    with ZipFile(WORD) as package:
        body = ET.fromstring(package.read('word/document.xml')).find(W + 'body')
    check(body is not None, 'Document body missing')
    started = False
    after_original = False
    paragraphs: list[tuple[str, str]] = []
    tables: list[list[list[str]]] = []
    appendix: list[str] = []
    appendix_tables: list[list[list[str]]] = []
    for element in body:
        if element.tag == W + 'p':
            text = text_of(element).strip()
            style = para_style(element)
            if text == '01. CRECISOL: identidad, visión, alcance y principios' and style == 'Heading1':
                started = True
                continue
            if not started:
                continue
            if text == 'Contrato de implementación y certificación del capítulo':
                after_original = True
            if after_original:
                if text.startswith('02.') and style == 'Heading1':
                    break
                appendix.append(text)
            elif style != 'OriginalNote':
                paragraphs.append((style, text))
        elif element.tag == W + 'tbl' and started:
            rows = []
            for tr in element.findall(W + 'tr'):
                rows.append([text_of(tc).strip() for tc in tr.findall(W + 'tc')])
            (appendix_tables if after_original else tables).append(rows)
    check('Contrato de implementación y certificación del capítulo' in appendix,
          'Appendix boundary not found')
    check(len(appendix_tables) == 2 and [len(t) for t in appendix_tables] == [3, 3],
          'Two supplementary tables of three rows each are required')
    additional = ' '.join(appendix)
    check('C01-01 Extraer / C01-02 Diseñar' in additional and
          'C01-05 Auditar A' in additional and 'C01-08 Acta' in additional,
          'Original eight-step gate sequence not found')
    check(all(label in additional for label in ('TEN-001', 'TEN-002', 'TEN-008',
          'TEN-009', 'TEN-010', 'TEN-021', 'TEN-032')),
          'Tenant checks missing from the supplemental source')
    return paragraphs, tables, digest


def main() -> None:
    paragraphs, tables, digest = source_parts()
    check(len(paragraphs) == 34,
          f'Expected 34 original paragraphs, got {len(paragraphs)}')
    headings = [style for style, _ in paragraphs if style.startswith('Heading')]
    check(len(headings) == 7, f'Expected 7 structural headings, got {len(headings)}')
    check(len(tables) == 3, f'Expected 3 original tables, got {len(tables)}')
    check([len(t) for t in tables] == [8, 9, 7],
          f'Expected 24 rows incl. headers [8,9,7], got {[len(t) for t in tables]}')
    check(all(len(row) == 3 for t in tables for row in t),
          'Original table structure is not three columns')
    check(all(text for _, text in paragraphs), 'Blank original paragraph')
    check(all(text for t in tables for row in t for text in row), 'Blank table cell')
    check('1.7. Pruebas de aceptación de marca e identidad digital'
          in [text for _, text in paragraphs], 'Acceptance section is missing')

    markdown = MATRIX.read_text(encoding='utf-8')
    paragraph_ids = re.findall(r'^\|\s*(P\d{2})\s*\|', markdown, flags=re.M)
    table_ids = re.findall(r'^\|\s*(T\d{2}-R\d{2})\s*\|', markdown, flags=re.M)
    expected_p = {f'P{i:02d}' for i in range(1, 35)}
    expected_t = {f'T{ti:02d}-R{ri:02d}' for ti, total in enumerate((7, 8, 6), 1)
                  for ri in range(1, total + 1)}
    check(set(paragraph_ids) == expected_p and len(paragraph_ids) == 34,
          f'Missing/duplicate paragraph refs: {sorted(expected_p ^ set(paragraph_ids))}; {Counter(paragraph_ids)}')
    check(set(table_ids) == expected_t and len(table_ids) == 21,
          f'Missing/duplicate table refs: {sorted(expected_t ^ set(table_ids))}; {Counter(table_ids)}')

    dependencies = DECISIONS.read_text(encoding='utf-8')
    for required in ('DIF-C01-01', 'C01-01 Extraer', 'C01-05 Auditar A',
                     'C01-06 Auditar B', 'C01-07 Auditar C', 'C01-08 Acta',
                     'Hostinger', 'NO VERIFICADO', 'TEN-001', 'P0', 'P1'):
        check(required in dependencies, f'Missing dependency/gate: {required}')

    print(f'PASS C01-01 SHA256={digest}')
    print('PASS 34 paragraphs: 27 content + 7 headings')
    print('PASS 3 original tables: 21 data rows + 3 headers')
    print('PASS 2 supplemental tables, original gate sequence, 7 TEN controls')
    print('PASS 55 unique matrix IDs, zero omissions/duplicates')
    print('PASS numbering discrepancy, dependencies and Hostinger uncertainty registered')
    print('LIMIT: source/coverage check only; not an implementation, security or certification audit')


if __name__ == '__main__':
    try:
        main()
    except (AssertionError, FileNotFoundError, KeyError, ValueError) as error:
        print(f'FAIL C01-01: {error}', file=sys.stderr)
        sys.exit(1)
