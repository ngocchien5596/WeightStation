$connectionString = "Server=.;Database=StationAppLocal;Trusted_Connection=True;Encrypt=False;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT name, OBJECT_NAME(parent_id) AS parent_table, is_disabled FROM sys.triggers"
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize

$conn.Close()
