with open(r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md', 'r', encoding='utf-8') as f:
    for idx, line in enumerate(f):
        if 'SyncStatus' in line or 'Sync Status' in line or 'sync_outbox' in line:
            print(f"Line {idx+1}: {line.strip()}")
