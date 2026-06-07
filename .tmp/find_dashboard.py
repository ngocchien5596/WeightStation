with open(r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md', 'r', encoding='utf-8') as f:
    for idx, line in enumerate(f):
        if 'DashboardViewModel' in line or 'thống kê' in line or 'weighing_sessions' in line:
            if 'truy vấn' in line or 'CreatedAt' in line or 'LoadCountersAsync' in line:
                print(f"Line {idx+1}: {line.strip()}")
