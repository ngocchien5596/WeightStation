with open(r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md', 'r', encoding='utf-8') as f:
    for idx, line in enumerate(f):
        lower_line = line.lower()
        if 'print' in lower_line or 'in' in lower_line:
            # check for specific print terms
            if any(term in lower_line for term in ['hasprinted', 'isprinted', 'ràng buộc', 'quy tắc', 'bắt buộc', 'in lại', 'cho xe ra']):
                print(f"Line {idx+1}: {line.strip()}")
