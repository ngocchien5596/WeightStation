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

# Restore and focus window
$showWindow = [ctypes.windll.user32]::ShowWindow
$setForegroundWindow = [ctypes.windll.user32]::SetForegroundWindow
# But since we are in PowerShell, we can use WScript.Shell to activate
$wshell = New-Object -ComObject WScript.Shell
$wshell.AppActivate($appWindow.Current.Name)
Start-Sleep -Milliseconds 500

$buttons = $appWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button))

# Helper to find and click button by Name substring
function Click-ButtonBySub($sub) {
    foreach ($btn in $buttons) {
        $name = $btn.Current.Name
        if ($name -like "*$sub*") {
            Write-Host "Invoking button: '$name'"
            $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $invokePattern.Invoke()
            return $true
        }
    }
    return $false
}

# 1. Capture Weighing View
Write-Host "Navigating to 'Cân nội địa'..."
if (Click-ButtonBySub "n?i d?a") {
    Start-Sleep -Seconds 1.5
    python scripts/capture_current.py weighing_view.png
}

# 2. Capture Export Weighing View
Write-Host "Navigating to 'Cân xuất khẩu'..."
if (Click-ButtonBySub "xu?t kh?u") {
    Start-Sleep -Seconds 1.5
    python scripts/capture_current.py export_weighing_view.png
}

# 3. Capture Outgoing Queue
Write-Host "Navigating to 'Danh sách xe ra'..."
if (Click-ButtonBySub "xe ra") {
    Start-Sleep -Seconds 1.5
    python scripts/capture_current.py outgoing_queue.png
}
