from __future__ import annotations

import base64
import traceback
from pathlib import Path

ASSETS = Path("src/KKL.WordStudio.UI/Assets/GuideScreens")
OUTPUT = Path("migration-output.txt")
ALLOWED_BASE64 = frozenset(
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/="
)


def decode_jpeg_resource(source: Path) -> bytes:
    text = source.read_text(encoding="utf-8-sig", errors="ignore")
    normalized = "".join(character for character in text if character in ALLOWED_BASE64)

    jpeg_start = normalized.find("/9j/")
    if jpeg_start < 0:
        raise RuntimeError(f"JPEG payload start was not found: {source}")
    normalized = normalized[jpeg_start:]

    padding_index = normalized.find("=")
    if padding_index >= 0:
        normalized = normalized[:padding_index]

    while normalized and len(normalized) % 4 == 1:
        normalized = normalized[:-1]
    normalized += "=" * ((4 - len(normalized) % 4) % 4)

    image = base64.b64decode(normalized, validate=False)
    jpeg_end = image.find(b"\xff\xd9", 2)
    if jpeg_end < 0:
        raise RuntimeError(f"JPEG end marker was not found: {source}")

    image = image[: jpeg_end + 2]
    if len(image) <= 1_000 or not image.startswith(b"\xff\xd8\xff"):
        raise RuntimeError(f"Decoded asset is not a valid JPEG: {source}")

    return image


def migrate() -> list[str]:
    sources = sorted(ASSETS.glob("*.jpg.base64"))
    if not sources:
        raise RuntimeError("No Base64 guide screenshots were found to migrate.")

    messages: list[str] = []
    for source in sources:
        image = decode_jpeg_resource(source)
        target = source.with_suffix("")
        target.write_bytes(image)
        source.unlink()
        messages.append(f"Migrated {source.name} -> {target.name} ({len(image)} bytes)")

    return messages


def main() -> int:
    try:
        messages = migrate()
        OUTPUT.write_text("\n".join(messages) + "\nSUCCESS\n", encoding="utf-8")
        for message in messages:
            print(message)
        return 0
    except Exception:
        details = traceback.format_exc()
        OUTPUT.write_text(details, encoding="utf-8")
        print(details)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
