with open(r'g:\Source-code\pmcan_C#\src\StationApp.UI\ViewModels\WeighingViewModel.cs', 'r', encoding='utf-8') as f:
    for idx, line in enumerate(f):
        if 'HasPrintedMasterWeighTicket' in line or 'HasPrintedDeliveryTicket' in line:
            print(f"Line {idx+1}: {line.strip()}")
