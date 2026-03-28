import csv
import json
import os
from typing import List, Optional, Sequence, Set

import requests


INDEX_URL = "https://api.scryfall.com/bulk-data"
BULK_TYPES = ("all_cards",)
TIMEOUT = 120


def sanitize(value: Optional[str]) -> str:
    if not value:
        return ""
    return " ".join(str(value).replace("\r", " ").replace("\n", " ").split())


def get_type_tokens(type_line: str) -> Set[str]:
    tokens: Set[str] = set()
    normalized = type_line.replace("â€”", "—")
    for face in normalized.split("//"):
        front = face
        if "—" in front:
            front = front.split("—", 1)[0]
        for token in front.split():
            token = token.strip()
            if token:
                tokens.add(token)
    return tokens


def infer_is_land(type_line: str) -> bool:
    if not type_line:
        return False

    non_land_core = {"Creature", "Artifact", "Enchantment", "Instant", "Sorcery", "Planeswalker", "Battle"}
    saw_land = False
    saw_non_land = False

    normalized = type_line.replace("â€”", "—")
    for face in normalized.split("//"):
        front = face
        if "—" in front:
            front = front.split("—", 1)[0]
        face_tokens = [t.strip() for t in front.split() if t.strip()]
        if not face_tokens:
            continue
        if "Land" in face_tokens:
            saw_land = True
        if any(token in non_land_core for token in face_tokens):
            saw_non_land = True

    return saw_land and not saw_non_land


def infer_category(type_line: str, is_land: bool) -> str:
    if is_land:
        return "Land"
    tokens = get_type_tokens(type_line)
    if "Creature" in tokens:
        return "Creature"
    if "Instant" in tokens or "Sorcery" in tokens:
        return "Instant/Sorcery"
    if "Artifact" in tokens:
        return "Artifact"
    if "Enchantment" in tokens:
        return "Enchantment"
    if "Planeswalker" in tokens:
        return "Planeswalker"
    if "Battle" in tokens:
        return "Battle"
    return "Other"


def infer_card_type(type_line: str, is_land: bool) -> str:
    if is_land:
        return "Land"
    tokens = list(get_type_tokens(type_line))
    if not tokens:
        return "Unknown"
    for preferred in ["Creature", "Instant", "Sorcery", "Artifact", "Enchantment", "Planeswalker", "Battle"]:
        if preferred in tokens:
            return preferred
    return tokens[0]


def get_colors(card: dict) -> str:
    colors = card.get("colors") or []
    if not colors and isinstance(card.get("card_faces"), list):
        combined: List[str] = []
        for face in card.get("card_faces", []):
            for color in (face.get("colors") or []):
                if color not in combined:
                    combined.append(color)
        colors = combined
    return ";".join(colors)


def get_color_identity(card: dict) -> str:
    identity = card.get("color_identity") or []
    return ";".join(sorted(set(str(c) for c in identity)))


def get_type_line(card: dict) -> str:
    type_line = sanitize(card.get("type_line"))
    if type_line:
        return type_line
    if isinstance(card.get("card_faces"), list):
        face_types = [sanitize(face.get("type_line")) for face in card["card_faces"]]
        face_types = [item for item in face_types if item]
        return " // ".join(face_types)
    return ""


def get_oracle_text(card: dict) -> str:
    oracle_text = sanitize(card.get("oracle_text"))
    if oracle_text:
        return oracle_text
    if isinstance(card.get("card_faces"), list):
        face_oracle = [sanitize(face.get("oracle_text")) for face in card["card_faces"]]
        face_oracle = [item for item in face_oracle if item]
        return " // ".join(face_oracle)
    return ""


def get_mana_value(card: dict, is_land: bool) -> float:
    if is_land:
        return 0.0
    cmc = card.get("cmc", 0)
    try:
        return float(cmc)
    except (TypeError, ValueError):
        return 0.0


def is_token_like_card(card: dict, type_line: str) -> bool:
    layout = sanitize(card.get("layout")).lower()
    set_type = sanitize(card.get("set_type")).lower()
    name = sanitize(card.get("name")).lower()
    type_lower = type_line.lower()

    token_layouts = {"token", "double_faced_token", "emblem", "art_series", "reversible_card"}
    if layout in token_layouts:
        return True

    if set_type == "token":
        return True

    if type_lower.startswith("token ") or " token " in f" {type_lower} ":
        return True

    if name.startswith("token "):
        return True

    return False


def infer_archetype_tags(type_line: str, oracle_text: str, is_land: bool) -> str:
    tags: Set[str] = set()
    tokens = get_type_tokens(type_line)
    oracle = oracle_text.lower()

    if is_land:
        tags.add("Land")

    if "Creature" in tokens:
        tags.add("Creature")
    if "Instant" in tokens:
        tags.add("Instant")
    if "Sorcery" in tokens:
        tags.add("Sorcery")
    if "Artifact" in tokens:
        tags.add("Artifact")
    if "Enchantment" in tokens:
        tags.add("Enchantment")
    if "Planeswalker" in tokens:
        tags.add("Planeswalker")
    if "Battle" in tokens:
        tags.add("Battle")

    if ("draw" in oracle and "card" in oracle) or "investigate" in oracle:
        tags.add("Card Draw")

    if (
        ("destroy" in oracle and "target" in oracle)
        or ("exile" in oracle and "target" in oracle)
        or ("fight" in oracle and "target" in oracle)
        or ("damage" in oracle and "target" in oracle)
        or "target player sacrifices" in oracle
        or "each opponent sacrifices" in oracle
    ):
        tags.add("Removal")

    if (
        "destroy all" in oracle
        or "exile all" in oracle
        or "return all" in oracle
        or "all creatures get -" in oracle
        or "each creature gets -" in oracle
    ):
        tags.add("Board Wipe")

    if ("add {" in oracle or "create a treasure" in oracle or "create treasure" in oracle) and not is_land:
        tags.add("Ramp")

    if (
        "search your library" in oracle
        or "search target player's library" in oracle
        or "reveal it and put it into your hand" in oracle
    ):
        tags.add("Tutor")

    if "counter target spell" in oracle or "counter up to" in oracle:
        tags.add("Counterspell")

    if "create" in oracle and "token" in oracle:
        tags.add("Token Generation")

    if (
        ("return target" in oracle or "put target" in oracle)
        and "from your graveyard" in oracle
    ):
        tags.add("Recursion")

    if "gain" in oracle and "life" in oracle:
        tags.add("Life Gain")

    if "discard" in oracle:
        tags.add("Discard")

    if "hexproof" in oracle or "indestructible" in oracle or "protection from" in oracle or "ward" in oracle:
        tags.add("Protection")

    if "can't" in oracle and ("cast" in oracle or "attack" in oracle or "activate" in oracle):
        tags.add("Stax")

    if not tags:
        tags.add("Other")

    return ", ".join(sorted(tags))


def get_dedupe_key(card: dict, name: str, type_line: str) -> str:
    oracle_id = sanitize(card.get("oracle_id")).lower()
    if oracle_id:
        return f"oracle:{oracle_id}"

    # Fallback for entries without oracle_id.
    return f"fallback:{name.lower()}|{type_line.lower()}"


def fetch_bulk_index() -> Sequence[dict]:
    print("Fetching Scryfall bulk data index...")
    response = requests.get(INDEX_URL, timeout=TIMEOUT)
    response.raise_for_status()
    payload = response.json()
    return payload.get("data", [])


def find_bulk_entry(index: Sequence[dict], bulk_type: str) -> dict:
    for item in index:
        if item.get("type") == bulk_type:
            return item
    raise RuntimeError(f"Could not locate '{bulk_type}' bulk data.")


def download_cards(download_uri: str) -> List[dict]:
    print(f"Downloading card dump from {download_uri}")
    response = requests.get(download_uri, timeout=TIMEOUT)
    response.raise_for_status()
    data = response.json()
    if not isinstance(data, list):
        raise RuntimeError("Bulk card payload was not a list.")
    return data


def main() -> None:
    script_dir = os.path.dirname(__file__)
    csv_path = os.path.join(script_dir, "cards.csv")
    json_path = os.path.join(script_dir, "all_cards.json")

    index = fetch_bulk_index()

    all_cards: List[dict] = []
    for bulk_type in BULK_TYPES:
        entry = find_bulk_entry(index, bulk_type)
        cards = download_cards(entry["download_uri"])
        all_cards.extend(cards)
        print(f"Loaded {len(cards):,} records from {bulk_type}")

    with open(json_path, "w", encoding="utf-8") as json_file:
        json.dump(all_cards, json_file, ensure_ascii=False)
    print(f"Saved merged bulk JSON to {json_path}")

    with open(csv_path, "w", encoding="utf-8", newline="") as csv_file:
        writer = csv.writer(csv_file, quoting=csv.QUOTE_MINIMAL)
        writer.writerow(["Name", "ManaCost", "Colors", "ColorIdentity", "Type", "Category", "CardType", "IsLand", "OracleText"])

        rows_written = 0
        skipped_tokens = 0
        skipped_duplicates = 0
        skipped_missing_name = 0
        seen_keys: Set[str] = set()

        for card in all_cards:
            name = sanitize(card.get("name"))
            if not name:
                skipped_missing_name += 1
                continue

            type_line = get_type_line(card)
            if is_token_like_card(card, type_line):
                skipped_tokens += 1
                continue

            dedupe_key = get_dedupe_key(card, name, type_line)
            if dedupe_key in seen_keys:
                skipped_duplicates += 1
                continue
            seen_keys.add(dedupe_key)

            is_land = infer_is_land(type_line)
            card_type = infer_card_type(type_line, is_land)
            oracle_text = get_oracle_text(card)
            category = infer_archetype_tags(type_line, oracle_text, is_land)
            colors = get_colors(card)
            mana_value = get_mana_value(card, is_land)

            color_identity = get_color_identity(card)
            writer.writerow([
                name,
                f"{mana_value:.1f}",
                colors,
                color_identity,
                type_line,
                category,
                card_type,
                "true" if is_land else "false",
                oracle_text,
            ])
            rows_written += 1

    print(f"Generated cards.csv with {rows_written:,} rows.")
    print(
        "CSV QA | "
        f"token-like skipped: {skipped_tokens:,} | "
        f"duplicates skipped: {skipped_duplicates:,} | "
        f"missing name skipped: {skipped_missing_name:,}"
    )


if __name__ == "__main__":
    main()
