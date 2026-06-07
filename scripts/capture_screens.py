import os
import time
import ctypes
from ctypes import wintypes
from PIL import ImageGrab

# Win32 APIs and Constants
user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, ctypes.c_void_p)

# ShowWindow commands
SW_RESTORE = 9

class RECT(ctypes.Structure):
    _fields_ = [
        ("left", ctypes.c_long),
        ("top", ctypes.c_long),
        ("right", ctypes.c_long),
        ("bottom", ctypes.c_long)
    ]

# Global dictionary to store found windows
found_windows = {}

def enum_windows_callback(hwnd, lParam):
    if user32.IsWindowVisible(hwnd):
        length = user32.GetWindowTextLengthW(hwnd)
        if length > 0:
            buffer = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buffer, length + 1)
            title = buffer.value
            # Match titles containing our app's keywords (case insensitive, including Unicode)
            title_lower = title.lower()
            if any(k in title_lower for k in ["trạm cân", "đăng nhập", "stationapp", "weightstation", "quan ly tram can", "dang nhap"]):
                found_windows[title] = hwnd
    return True

def find_app_window():
    found_windows.clear()
    user32.EnumWindows(WNDENUMPROC(enum_windows_callback), 0)
    return found_windows

def capture_window(hwnd, output_path):
    # Restore if minimized
    user32.ShowWindow(hwnd, SW_RESTORE)
    time.sleep(0.1)
    
    # Bring to front
    user32.SetForegroundWindow(hwnd)
    time.sleep(0.5)  # Wait for rendering and focus transition
    
    rect = RECT()
    if user32.GetWindowRect(hwnd, ctypes.byref(rect)):
        bbox = (rect.left, rect.top, rect.right, rect.bottom)
        print(f"Chup vung man hinh: {bbox}")
        
        try:
            if rect.right > rect.left and rect.bottom > rect.top:
                img = ImageGrab.grab(bbox)
                os.makedirs(os.path.dirname(output_path), exist_ok=True)
                img.save(output_path, "PNG")
                print(f"Da luu anh thanh cong: {output_path}")
                return True
            else:
                print("Loi: Kich thuoc cua so khong hop le.")
        except Exception as e:
            print(f"Loi khi chup man hinh: {e}")
    else:
        print("Loi: Khong lay duoc toa do cua so.")
    return False

def main():
    print("="*60)
    print(" KICH BAN CHUP ANH GIAO DIEN HE THONG TRAM CAN (WEIGHTSTATION) ")
    print("="*60)
    
    screens = [
        ("login_window.png", "Man hinh Dang nhap (LoginWindow.xaml)"),
        ("dashboard_view.png", "Man hinh Trang chu / Dashboard (DashboardView.xaml)"),
        ("incoming_queue.png", "Man hinh Danh sach xe vao (IncomingVehicleListView.xaml)"),
        ("weighing_view.png", "Man hinh Can noi dia chinh (WeighingView.xaml)"),
        ("vehicle_select_dialog.png", "Dialog con: Chon dai dien xe (VehicleRepresentativeSelectionDialogWindow.xaml)"),
        ("print_options_dialog.png", "Dialog con: Cau hinh in phieu (PrintOptionsDialogWindow.xaml)"),
        ("camera_history_dialog.png", "Dialog con: Xem anh camera lich su (CameraImageHistoryWindow.xaml)"),
        ("export_weighing_view.png", "Man hinh Can xuat khau don lon (ExportWeighingView.xaml)"),
        ("export_transfer_dialog.png", "Dialog con: Chuyen chuyen xe con xuat khau (ExportTripTransferDialogWindow.xaml)"),
        ("outgoing_queue.png", "Man hinh Danh sach xe ra & In lai phieu (OutgoingVehicleListView.xaml)"),
        ("inbound_report_view.png", "Man hinh Bao cao nhap hang (InboundSummaryReportView.xaml)"),
        ("export_report_view.png", "Man hinh Bao cao xuat khau (ExportSummaryReportView.xaml)"),
        ("scale_config_view.png", "Tab cau hinh: Cau hinh thiet bi can cong COM (ScaleDeviceConfigView.xaml)"),
        ("camera_config_view.png", "Tab cau hinh: Cau hinh camera IP RTSP (CameraConfigView.xaml)"),
        ("print_config_view.png", "Tab cau hinh: Cau hinh phoi in an (PrintConfigView.xaml)"),
        ("account_config_view.png", "Tab cau hinh: Quan ly tai khoan nguoi dung (AccountManagementView.xaml)"),
        ("system_config_view.png", "Tab cau hinh: Cau hinh he thong & Sao luu (SystemSettingsView.xaml)"),
        ("sync_config_view.png", "Tab cau hinh: Trang thai dong bo hang cho Outbox (SyncInfoView.xaml)"),
        ("product_master_view.png", "Tab cau hinh: Danh muc san pham (ProductMasterView.xaml)"),
        ("customer_master_view.png", "Tab cau hinh: Danh muc khach hang (CustomerMasterView.xaml)"),
        ("vehicle_master_view.png", "Tab cau hinh: Danh muc phuong tien (VehicleMasterView.xaml)"),
        ("diagnostics_view.png", "Man hinh Chan doan cong COM & camera (DiagnosticsView.xaml)"),
        ("ticket_list_view.png", "Man hinh Danh sach phieu can (TicketListView.xaml)"),
        ("app_update_view.png", "Man hinh Cap nhat phien ban (AppUpdateView.xaml)")
    ]
    
    images_dir = r"g:\Source-code\pmcan_C#\SRSdocs\images"
    
    print("\nBuoc 1: Tim kiem cua so ung dung tram can dang chay...")
    windows = find_app_window()
    if not windows:
        print("Khong tim thay cua so ung dung nao khop (Dang chay hoac dang mo).")
        print("Vui long dam bao ung dung WPF dang hoat dong tren man hinh desktop.")
        return
    
    print("\nTim thay cac cua so kha dung:")
    win_list = list(windows.items())
    for idx, (title, hwnd) in enumerate(win_list):
        # Clean title for display to prevent encoding issues in cmd/powershell
        clean_title = title.encode('ascii', 'ignore').decode('ascii')
        if not clean_title.strip():
            clean_title = f"Window HWND {hwnd}"
        print(f"[{idx}] Tieu de: \"{clean_title}\" (HWND: {hwnd})")
        
    choice = 0
    if len(win_list) > 1:
        try:
            choice = int(input(f"Chon cua so de chup (0-{len(win_list)-1}): "))
        except ValueError:
            choice = 0
            
    selected_title, selected_hwnd = win_list[choice]
    clean_sel_title = selected_title.encode('ascii', 'ignore').decode('ascii')
    print(f"\nBat dau chup tu cua so: \"{clean_sel_title}\"\n")
    
    for filename, description in screens:
        output_path = os.path.join(images_dir, filename)
        print("-" * 50)
        print(f"Yeu cau: {description}")
        print(f"--> File anh se luu: {filename}")
        print("Vui long CHUYEN ung dung sang giao dien nay.")
        val = input("Nhan ENTER de CHUP (hoac nhap 's' de BO QUA man hinh nay): ")
        if val.strip().lower() == 's':
            print("Da bo qua.")
            continue
            
        success = capture_window(selected_hwnd, output_path)
        if not success:
            print("Loi chup hinh. Thu lai...")
            val = input("Nhan ENTER de thu chup lai cua so: ")
            capture_window(selected_hwnd, output_path)
            
    print("\n" + "="*50)
    print("HOAN THANH QUA TRINH CHUP ANH GIAO DIEN!")
    print(f"Cac anh chup duoc luu tru trong thu muc: {images_dir}")
    print("="*50)

if __name__ == "__main__":
    main()
