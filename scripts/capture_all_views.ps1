Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)

$appWindow = $null
foreach ($w in $windows) {
    $title = $w.Current.Name
    if ($title -like "*trạm cân*" -or $title -like "*tram can*" -or $title -like "*XMCP*") {
        $appWindow = $w
        break
    }
}

if ($appWindow -eq $null) {
    Write-Host "WPF Window not found."
    exit
}

# Focus window
$wshell = New-Object -ComObject WScript.Shell
$wshell.AppActivate($appWindow.Current.Name)
Start-Sleep -Milliseconds 500

# Helper to find and click a button by Name substring
function Click-ButtonByName($sub) {
    # Re-fetch buttons list to handle dynamic additions/removals
    $buttons = $appWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button))
    foreach ($btn in $buttons) {
        $name = $btn.Current.Name
        if ($name -like "*$sub*") {
            Write-Host "Clicking button: '$name'"
            $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $invokePattern.Invoke()
            Start-Sleep -Seconds 1.5
            return $true
        }
    }
    Write-Host "Button like '$sub' not found."
    return $false
}

# Helper to click a button by Index
function Click-ButtonByIndex($idx) {
    $buttons = $appWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button))
    if ($idx -lt $buttons.Count) {
        $btn = $buttons[$idx]
        $name = $btn.Current.Name
        Write-Host "Clicking button at index ${idx}: '$name'"
        $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invokePattern.Invoke()
        Start-Sleep -Seconds 1.5
        return $true
    }
    Write-Host "Button index ${idx} out of range."
    return $false
}

# 1. Capture Weighing View
Write-Host "1. Navigating to Cân nội địa..."
if (Click-ButtonByIndex 3) {
    python scripts/capture_current.py weighing_view.png
}

# 2. Capture Reports
Write-Host "2. Expanding Reports menu..."
if (Click-ButtonByIndex 6) {
    # Now click Reports_ExportSummary (Báo cáo xuất)
    Write-Host "Clicking Báo cáo xuất..."
    if (Click-ButtonByName "xu?t") {
        python scripts/capture_current.py export_report_view.png
    }
    # Click Reports_InboundSummary (Báo cáo nhập)
    Write-Host "Clicking Báo cáo nhập..."
    if (Click-ButtonByName "nhap" -or Click-ButtonByName "nh\u1eadp") {
        python scripts/capture_current.py inbound_report_view.png
    }
}

# 3. Capture Settings Tabs
Write-Host "3. Expanding Settings menu..."
if (Click-ButtonByIndex 7) {
    $settingsTabs = @(
        ("C\u1eadp nh\u1eadt", "app_update_view.png"), # AppUpdate
        ("tham s\u1ed1", "system_config_view.png"),   # Settings_Params (Cấu hình hệ thống & sao lưu)
        ("camera", "camera_config_view.png"),         # Settings_Camera (Cấu hình camera)
        ("thi\u1ebft b\u1ecb", "scale_config_view.png"), # Settings_Device (Cấu hình thiết bị cân)
        ("ph\u00f4i in", "print_config_view.png"),     # Settings_Print (Cấu hình in)
        ("ph\u01b0\u01a1ng ti\u1ec7n", "vehicle_master_view.png"), # Settings_Vehicles
        ("kh\u00e1ch h\u00e0ng", "customer_master_view.png"),       # Settings_Customers
        ("s\u1ea3n ph\u1ea9m", "product_master_view.png"),           # Settings_Products
        ("\u0111\u1ed3ng b\u1ed9", "sync_config_view.png"),          # Settings_Sync
        ("t\u00e0i kho\u1ea3n", "account_config_view.png")          # Settings_Accounts
    )
    
    foreach ($tab in $settingsTabs) {
        $search = $tab[0]
        $file = $tab[1]
        $decodedSearch = [regex]::Unescape($search)
        Write-Host "Navigating to settings tab matching: $decodedSearch -> $file"
        
        if (Click-ButtonByName $decodedSearch) {
            python scripts/capture_current.py $file
        }
    }
}

Write-Host "Done capturing all accessible main views!"
