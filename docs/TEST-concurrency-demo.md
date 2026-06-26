# Test Concurrency Sinh Chứng Từ - Demo Đơn Giản

## 🎯 Minh họa Atomic Counter service

Dưới đây là code demo đơn giản để test tính atomic của DocumentCounterService:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Tests;

public class DocumentCounterConcurrencyDemo
{
    public static async Task Main()
    {
        // Setup host (như SyncTests.cs)
        var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder([])
            .ConfigureServices((context, services) =>
            {
                // Add services
                services.AddInfrastructure();
            })
            .Build();

        var scope = host.Services.CreateScope();
        var counterService = scope.ServiceProvider.GetRequiredService<IDocumentCounterService>();

        // Test 50 concurrent requests
        var counterKey = $"Test_Concurrency_{DateTime.UtcNow:yyyyMMddHHmmss}";

        var tasks = Enumerable.Range(1, 50).Select(i => Task.Run(async () =>
        {
            return await counterService.GetNextSequenceAsync(counterKey, CancellationToken.None);
        }));

        var sequences = await Task.WhenAll(tasks);

        // Kết quả
        Console.WriteLine($"✅ Total requests: {sequences.Length}");
        Console.WriteLine($"✅ Unique values: {sequences.Distinct().Count()}");
        Console.WriteLine($"✅ Min value: {sequences.Min()}");
        Console.WriteLine($"✅ Max value: {sequences.Max()}");
        Console.WriteLine($"✅ Sequential check: {(sequences.OrderBy(s => s).SequenceEqual(sequences) ? "PASS" : "FAIL")}");
    }
}
```

## 📊 Kết quả mong đợi khi chạy:

```
✅ Total requests: 50
✅ Unique values: 50
✅ Min value: 1
✅ Max value: 50
✅ Sequential check: PASS
```

## 🔬 Test 2 User cùng lúc query counter từ SQL Server

```sql
-- Session 1 (User A):
BEGIN TRAN;
UPDATE dbo.document_counters WITH (ROWLOCK)
SET LastValue = LastValue + 1
OUTPUT inserted.LastValue
WHERE CounterKey = 'Test_LC2606';
-- Result: 1001
-- Giữ nguyên transaction (KHÔNG COMMIT)

-- Session 2 (User B) - Cùng lúc:
UPDATE dbo.document_counters WITH (ROWLOCK)
SET LastValue = LastValue + 1
OUTPUT inserted.LastValue
WHERE CounterKey = 'Test_LC2606';
-- BLOCK ⏳ - Chờ Session 1 release

-- Session 1:
COMMIT; -- Release lock 🔒

-- Session 2 - Tiếp tục:
-- Result: 1002 (ROPWLOCK được release, increment tiếp)
COMMIT;
```

## ✅ Kết luận:

**Option 1 (Sinh số TRONG transaction)** hoạt động đúng:

```
User A: BEGIN → GetNextSequence → 1001 → INSERT → COMMIT ✅
User B:            (BLOCK) → GetNextSequence → 1002 → INSERT → COMMIT ✅

Kết quả:
- User A: Số 1001 ✅
- User B: Số 1002 ✅  
- Liên tiếp: 1001, 1002 (không gap) ✅
- Không duplicate: ROWLOCK bảo vệ ✅
```

```
User A: BEGIN → GetNextSequence → 1001 → INSERT → ROLLBACK ❌
        (Counter rollback về 1000 ? - TÙY transaction)

User B:            (UNBLOCK sau A rollback) → GetNextSequence → 1001 → INSERT → COMMIT ✅

Kết quả:
- User A: KHÔNG có session (rollback) ✅
- User B: Số 1001 (sử dụng lại) ✅
- Không gap: 1001 được dùng ✅
- Không duplicate: UNIQUE INDEX bảo vệ ✅
```