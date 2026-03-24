import json
import os
import requests

# 1. Download metadata for bulk data
index_url = 'https://api.scryfall.com/bulk-data'
print('Fetching Scryfall bulk data index...')
index = requests.get(index_url).json()

all_data = None
for item in index['data']:
    if item['type'] == 'default_cards':
        all_data = item
        break

if all_data is None:
    raise SystemExit('Could not locate default_cards bulk data.')

bulk_url = all_data['download_uri']
print('Downloading card dump from', bulk_url)
resp = requests.get(bulk_url, stream=True)
resp.raise_for_status()

json_path = os.path.join(os.path.dirname(__file__), 'all_cards.json')
with open(json_path, 'wb') as f:
    for chunk in resp.iter_content(chunk_size=8192):
        f.write(chunk)

print('Downloaded all_cards.json')

# 2. Parse and write local CSV
csv_path = os.path.join(os.path.dirname(__file__), 'cards.csv')

with open(json_path, 'r', encoding='utf-8') as f:
    cards = json.load(f)

with open(csv_path, 'w', encoding='utf-8') as f:
    f.write('Name,ManaCost,Colors,Type,Category,IsLand\n')
    for c in cards:
        name = c.get('name', '').replace('"', '').replace('\n', ' ').strip()
        mana_cost = c.get('cmc', 0)
        colors = ';'.join(c.get('colors', []))
        type_line = c.get('type_line', '').replace(',', ' ').strip()
        category = 'Land' if 'Land' in type_line else (
            'Creature' if 'Creature' in type_line else (
            'Instant/Sorcery' if 'Instant' in type_line or 'Sorcery' in type_line else (
            'Artifact' if 'Artifact' in type_line else (
            'Enchantment' if 'Enchantment' in type_line else (
            'Planeswalker' if 'Planeswalker' in type_line else 'Other')))))
        is_land = 'true' if 'Land' in type_line else 'false'
        # Escaping commas by replacing with space in name and type
        clean_name = name.replace(',', ' ') 
        clean_type = type_line.replace(',', ' ')
        f.write(f'{clean_name},{mana_cost},{colors},{clean_type},{category},{is_land}\n')

print('Generated cards.csv (all cards).')
