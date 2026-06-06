$connectionString = "Server=.;Database=StationAppLocal;Trusted_Connection=True;Encrypt=False;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT * FROM cut_orders WHERE Id = 'e993f86c-be97-47dc-878c-733cd2a9bfcf'"
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-List

$conn.Close()
