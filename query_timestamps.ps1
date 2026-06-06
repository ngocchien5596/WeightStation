$connectionString = "Server=.;Database=StationAppLocal;Trusted_Connection=True;Encrypt=False;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

$cmd = $conn.CreateCommand()
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)

# 1. Weighing Sessions
Write-Output "=== WEIGHING SESSIONS ==="
$cmd.CommandText = "SELECT Id, SessionNo, SessionStatus, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM weighing_sessions WHERE SessionNo IN ('LC26060024', 'LC26060026')"
$dtSess = New-Object System.Data.DataTable
$adapter.Fill($dtSess) | Out-Null
$dtSess | Format-Table -AutoSize

# 2. Cut Orders
Write-Output "=== CUT ORDERS ==="
$cmd.CommandText = "SELECT Id, ErpCutOrderId, CutOrderStatus, ProcessingStage, WeighingSessionId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM cut_orders WHERE Id = 'e993f86c-be97-47dc-878c-733cd2a9bfcf'"
$dtCut = New-Object System.Data.DataTable
$adapter.Fill($dtCut) | Out-Null
$dtCut | Format-Table -AutoSize

# 3. Weighing Session Lines
Write-Output "=== WEIGHING SESSION LINES ==="
$cmd.CommandText = "SELECT Id, WeighingSessionId, CutOrderId, SequenceNo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted FROM weighing_session_lines WHERE Id IN ('2702a90f-686f-4e8d-bd6d-d820017a02c5', '023dcb35-2599-4990-b4bd-906c2fae7f06')"
$dtLines = New-Object System.Data.DataTable
$adapter.Fill($dtLines) | Out-Null
$dtLines | Format-Table -AutoSize

$conn.Close()
