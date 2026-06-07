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
Start-Sleep -Milliseconds 800

# Helper to find and click a button by text (checks exact match first, then substring)
function Click-ButtonByText($targetUnicode) {
    $targetText = [regex]::Unescape($targetUnicode)
    Write-Host "Searching for button: '$targetText'..."
    
    $buttons = $appWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button))
    
    # 1. Exact match on Button Name or Child TextBlock Name
    foreach ($btn in $buttons) {
        $btnName = $btn.Current.Name
        if ($btnName -and ($btnName -eq $targetText)) {
            Write-Host "Found button by exact name: '$btnName'"
            $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $invokePattern.Invoke()
            Start-Sleep -Seconds 1.5
            return $true
        }
        
        $textblocks = $btn.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text))
        foreach ($tb in $textblocks) {
            $tbName = $tb.Current.Name
            if ($tbName -and ($tbName -eq $targetText)) {
                Write-Host "Found button by exact textblock name: '$tbName'"
                $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
                $invokePattern.Invoke()
                Start-Sleep -Seconds 1.5
                return $true
            }
        }
    }
    
    # 2. Substring match fallback (for cases where exact fails due to trailing spaces or icons)
    foreach ($btn in $buttons) {
        $btnName = $btn.Current.Name
        if ($btnName -and ($btnName -like "*$targetText*")) {
            Write-Host "Found button by substring name: '$btnName'"
            $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $invokePattern.Invoke()
            Start-Sleep -Seconds 1.5
            return $true
        }
        
        $textblocks = $btn.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text))
        foreach ($tb in $textblocks) {
            $tbName = $tb.Current.Name
            if ($tbName -and ($tbName -like "*$targetText*")) {
                Write-Host "Found button by substring textblock: '$tbName'"
                $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
                $invokePattern.Invoke()
                Start-Sleep -Seconds 1.5
                return $true
            }
        }
    }
    
    Write-Host "Button '$targetText' not found."
    return $false
}

# 1. Capture Dashboard & Operational screens
$mainScreens = @(
    ("Trang ch\u1ee7", "dashboard_view.png"),
    ("Danh s\u00e1ch xe v\u00e0o", "incoming_queue.png"),
    ("C\u00e2n n\u1ed9i \u0111\u1ecba", "weighing_view.png"),
    ("C\u00e2n xu\u1ea5t kh\u1ea5u", "export_weighing_view.png"),
    ("Danh s\u00e1ch xe ra", "outgoing_queue.png")
)

foreach ($scr in $mainScreens) {
    $text = $scr[0]
    $file = $scr[1]
    if (Click-ButtonByText $text) {
        python scripts/capture_current.py $file
    }
}

# 2. Capture Reports Sub-items
Write-Host "Expanding Reports Submenu..."
if (Click-ButtonByText "B\u00e1o c\u00e1o") {
    if (Click-ButtonByText "B\u00e1o c\u00e1o xu\u1ea5t") {
        python scripts/capture_current.py export_report_view.png
    }
    if (Click-ButtonByText "B\u00e1o c\u00e1o nh\u1eadp") {
        python scripts/capture_current.py inbound_report_view.png
    }
    # Collapse Reports menu
    Click-ButtonByText "B\u00e1o c\u00e1o" | Out-Null
}

# 3. Capture Settings Sub-items
Write-Host "Expanding Settings Submenu..."
if (Click-ButtonByText "C\u1ea5u h\u00ecnh") {
    
    $settingsTabs = @(
        ("C\u1eadp nh\u1eadt \u1ee9ng d\u1ee5ng", "app_update_view.png"),
        ("C\u1ea5u h\u00ecnh h\u1ec7 th\u1ed1ng", "system_config_view.png"),
        ("C\u1ea5u h\u00ecnh camera", "camera_config_view.png"),
        ("C\u1ea5u h\u00ecnh thi\u1ebft b\u1ecb", "scale_config_view.png"),
        ("C\u1ea5u h\u00ecnh ph\u00f4i in", "print_config_view.png"),
        ("Danh m\u1ee5c ph\u01b0\u01a1ng ti\u1ec7n", "vehicle_master_view.png"),
        ("Danh m\u1ee5c kh\u00e1ch h\u00e0ng", "customer_master_view.png"),
        ("Danh m\u1ee5c s\u1ea3n ph\u1ea9m", "product_master_view.png"),
        ("Th\u00f4ng tin \u0111\u1ed3ng b\u1ed9", "sync_config_view.png"),
        ("Qu\u1ea3n l\u00fd t\u00e0i kho\u1ea3n", "account_config_view.png")
    )
    
    foreach ($tab in $settingsTabs) {
        $search = $tab[0]
        $file = $tab[1]
        if (Click-ButtonByText $search) {
            python scripts/capture_current.py $file
        }
    }
    # Collapse Settings menu
    Click-ButtonByText "C\u1ea5u h\u00ecnh" | Out-Null
}

Write-Host "All captures complete!"
