import re
from pathlib import Path

PROJECT_ROOT = Path(".")
OUTPUT_FILE = Path("combined.cpp")

def extract_prefix(filename):
    match = re.match(r"(\d+)_", filename.name)
    return int(match.group(1)) if match else 999

def collect_files():
    all_hpps = list(Path(".").rglob("*.hpp"))
    all_cpps = list(Path(".").rglob("*.cpp"))

    hpps = sorted([f for f in all_hpps if f.name != OUTPUT_FILE.name], key=extract_prefix)
    cpps = sorted([f for f in all_cpps if f.name != OUTPUT_FILE.name], key=extract_prefix)

    main_cpp = [f for f in cpps if f.name.endswith("main.cpp")]
    other_cpp = [f for f in cpps if f not in main_cpp]
    return hpps, other_cpp, main_cpp

def clean_lines(lines, includes_set):
    cleaned = []
    for line in lines:
        stripped = line.strip()

        if stripped == "#pragma once":
            continue
        if stripped.startswith("#include <"):
            includes_set.add(stripped)
            continue
        if stripped.startswith("#include \""):
            continue

        cleaned.append(line)
    return cleaned

def merge_files(hpps, cpps, main, output_path):
    includes = set()
    code_blocks = []

    for section, files in [
        ("HEADER FILES", hpps),
        ("CPP FILES", cpps),
        ("MAIN FILE", main)
    ]:
        block = [f"// === {section} ===\n"]
        for f in files:
            block.append(f"\n// --- {f.name} ---\n")
            lines = f.read_text(encoding="utf-8").splitlines(keepends=True)
            cleaned = clean_lines(lines, includes)
            block.extend(cleaned)
        code_blocks.append(block)

    with open(output_path, "w", encoding="utf-8") as out:
        out.write("// === STANDARD INCLUDES ===\n")
        for inc in sorted(includes):
            out.write(inc + "\n")

        for block in code_blocks:
            out.writelines(block)

    print(f"✅ Merged into: {output_path}")

if __name__ == "__main__":
    hpps, cpps, main = collect_files()
    merge_files(hpps, cpps, main, OUTPUT_FILE)
