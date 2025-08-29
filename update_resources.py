import csv
import xml.etree.ElementTree as ET
from pathlib import Path

CSV_FILE = 'translation_table.csv'
LANG_MAP = {
    'ru': 'Russian',
    'en-US': 'English',
    'de-DE': 'German',
    'et': 'Estonian',
    'fr-FR': 'French',
    'pl-PL': 'Polish',
    'tr-TR': 'Turkish',
    'zh-CN': 'ChineseSimplified',
}

def load_translations(path):
    translations = {}
    with open(path, encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            key = row['Key']
            for lang, value in row.items():
                if lang == 'Key':
                    continue
                translations.setdefault(lang, {})[key] = value
    return translations

def update_resx(path, lang, tr_map):
    tree = ET.parse(path)
    root = tree.getroot()
    updated = False
    for data in root.findall('data'):
        key = data.get('name')
        if key and key in tr_map:
            val = tr_map[key]
            if val:
                value_elem = data.find('value')
                if value_elem is not None and value_elem.text != val:
                    value_elem.text = val
                    updated = True
    if updated:
        ET.indent(tree, space="  ")
        tree.write(path, encoding='utf-8', xml_declaration=True)
        print(f"Updated {path}")

if __name__ == '__main__':
    translations = load_translations(CSV_FILE)
    for file in Path('Resources').glob('SharedResource.*.resx'):
        lang_code = file.stem.split('.')[-1]
        lang_name = LANG_MAP.get(lang_code)
        if not lang_name:
            continue
        tr_map = translations.get(lang_name)
        if not tr_map:
            continue
        update_resx(file, lang_name, tr_map)
