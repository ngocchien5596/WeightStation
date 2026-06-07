with open(r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md', 'r', encoding='utf-8') as f:
    for idx, line in enumerate(f):
        if 'Ràng buộc in ấn' in line or 'Quy tắc In ấn Bắt buộc' in line or 'vượt quá ngưỡng dung sai' in line:
            print(f"Line {idx+1}: {line.strip()}")
