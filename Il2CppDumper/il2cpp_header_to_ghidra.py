import argparse
import re

header = "typedef unsigned __int8 uint8_t;\n" \
         "typedef unsigned __int16 uint16_t;\n" \
         "typedef unsigned __int32 uint32_t;\n" \
         "typedef unsigned __int64 uint64_t;\n" \
         "typedef __int8 int8_t;\n" \
         "typedef __int16 int16_t;\n" \
         "typedef __int32 int32_t;\n" \
         "typedef __int64 int64_t;\n" \
         "typedef __int64 intptr_t;\n" \
         "typedef unsigned __int64 uintptr_t;\n" \
         "typedef unsigned __int64 size_t;\n" \
         "typedef _Bool bool;\n"


def main():
    parser = argparse.ArgumentParser(description="Convert il2cpp.h for Ghidra.")
    parser.add_argument('--bits', type=int, choices=(32, 64), help="Pointer width for older headers without an architecture marker (default: 64).")
    args = parser.parse_args()
    fixed_header_data = ""
    with open("il2cpp.h", 'r', encoding='utf-8-sig') as f:
        print("il2cpp.h opened...")
        original_header_data = f.read()
        marker = re.search(r'// Native layout: metadata [^\r\n]*, (32|64)-bit\.', original_header_data)
        detected_bits = int(marker.group(1)) if marker else None
        if args.bits and detected_bits and args.bits != detected_bits:
            parser.error('--bits does not match the input header architecture')
        bits = args.bits or detected_bits or 64
        print("il2cpp.h read...")
        fixed_header_data = re.sub(r": (\w+) {", r"{\n \1 super;", original_header_data)
        print("il2cpp.h data fixed...")
    print("il2cpp.h closed.")
    with open("il2cpp_ghidra.h", 'w', encoding='utf-8') as f:
        print("il2cpp_ghidra.h opened...")
        native_header = header
        if bits == 32:
            native_header = native_header.replace('__int64 intptr_t;', '__int32 intptr_t;')
            native_header = native_header.replace('__int64 uintptr_t;', '__int32 uintptr_t;')
            native_header = native_header.replace('__int64 size_t;', '__int32 size_t;')
        f.write(native_header)
        print("header written...")
        f.write(fixed_header_data)
        print("fixed data written...")
    print("il2cpp_ghidra.h closed.")


if __name__ == '__main__':
    print("Script started...")
    main()
