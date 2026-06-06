$connectionString = "Server=.;Database=StationAppLocal;Trusted_Connection=True;Encrypt=False;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

$cmd = $conn.CreateCommand()
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)

# 1. Count rows
$cmd.CommandText = "SELECT COUNT(*) FROM audit_logs"
$count = $cmd.ExecuteScalar()
Write-Output "Total rows in audit_logs: $count"

# 2. Get top 20 rows
if ($count -gt 0) {
    Write-Output "--- TOP 20 AUDIT LOGS ---"
    $cmd.CommandText = "SELECT TOP 20 * FROM audit_logs ORDER BY CreatedAt DESC"
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null
    $dt | Format-List
}

$conn.Close()
