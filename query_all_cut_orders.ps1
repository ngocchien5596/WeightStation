$connectionString = "Server=.;Database=StationAppLocal;Trusted_Connection=True;Encrypt=False;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, ErpCutOrderId, CutOrderStatus, ProcessingStage, WeighingSessionId, IsDeleted, DeletedAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM cut_orders WHERE ErpCutOrderId = 'QN.CL.2606/0029'"
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-List

$conn.Close()
