import os
import sys
import time
import ctypes
from ctypes import wintypes
from PIL import ImageGrab

user32 = ctypes.windll.user32
WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, ctypes.c_void_p)
SW_RESTORE = 9

class RECT(ctypes.Structure):
    _fields_ = [
        ("left", ctypes.c_long),
        ("top", ctypes.c_long),
        ("right", ctypes.c_long),
        ("bottom", ctypes.c_long)
    ]

found_windows = {}

def enum_windows_callback(hwnd, lParam):
    if user32.IsWindowVisible(hwnd):
        length = user32.GetWindowTextLengthW(hwnd)
        if length > 0:
            buffer = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buffer, length + 1)
            title = buffer.value
            title_lower = title.lower()
            if any(k in title_lower for k in ["trạm cân", "weightstation", "xmcp"]):
                if not any(exclude in title_lower for exclude in ["antigravity", "visual studio", "vscode", "ssms", "chrome", "edge", "notepad"]):
                    found_windows[title] = hwnd
    return True

def main():
    if len(sys.argv) < 2:
        print("Usage: python capture_current.py <filename.png>")
        return
        
    filename = sys.argv[1]
    
    user32.EnumWindows(WNDENUMPROC(enum_windows_callback), 0)
    if not found_windows:
        print("No app window found.")
        return
        
    title, hwnd = list(found_windows.items())[0]
    # Clean title for print
    clean_title = title.encode('ascii', 'ignore').decode('ascii')
    print(f"Capturing: {clean_title} -> {filename}")
    
    # Restore and focus
    user32.ShowWindow(hwnd, SW_RESTORE)
    time.sleep(0.1)
    user32.SetForegroundWindow(hwnd)
    time.sleep(0.5)
    
    rect = RECT()
    if user32.GetWindowRect(hwnd, ctypes.byref(rect)):
        bbox = (rect.left, rect.top, rect.right, rect.bottom)
        img = ImageGrab.grab(bbox)
        output_dir = r"g:\Source-code\pmcan_C#\SRSdocs\images"
        os.makedirs(output_dir, exist_ok=True)
        img.save(os.path.join(output_dir, filename), "PNG")
        print(f"Captured and saved to {filename}")
    else:
        print("Failed to get window rect.")

if __name__ == "__main__":
    main()
