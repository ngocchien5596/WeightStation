Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)

$appWindow = $null
foreach ($w in $windows) {
    $title = $w.Current.Name
    if ($title -like "*trạm cân*" -or $title -like "*tram can*" -or $title -like "*XMCP*") {
        $appWindow = $w
        Write-Host "Found WPF Window: $title"
        break
    }
}

if ($appWindow -eq $null) {
    Write-Host "WPF Window not found."
    exit
}

$buttons = $appWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button))

Write-Host "Found $($buttons.Count) buttons:"
foreach ($btn in $buttons) {
    $name = $btn.Current.Name
    $id = $btn.Current.AutomationId
    Write-Host " - Name: '$name', AutomationId: '$id'"
}
