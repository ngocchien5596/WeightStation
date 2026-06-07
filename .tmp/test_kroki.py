import base64
import zlib
import urllib.request
import ssl

def encode_kroki(text):
    # Compress using zlib and encode to URL-safe base64
    # Kroki expects standard deflate compression. 
    # In Python, zlib.compress creates standard zlib data. 
    # We can use zlib.compress and then base64url.
    compressed = zlib.compress(text.encode('utf-8'))
    return base64.urlsafe_b64encode(compressed).decode('utf-8')

def test():
    state_machine_code = """stateDiagram-v2
    [*] --> PENDING_WEIGHT1 : Tạo phiên cân từ Danh sách xe vào
    PENDING_WEIGHT1 --> PENDING_WEIGHT2 : Lưu cân lần 1 thành công (Weight 1)
    PENDING_WEIGHT2 --> ALLOCATION_PENDING : Lưu cân lần 2 thành công (Weight 2)
    ALLOCATION_PENDING --> READY_TO_COMPLETE : Xác nhận phân bổ thực tế thành công
    READY_TO_COMPLETE --> COMPLETED : In đủ Phiếu cân tổng hợp chính & tất cả phiếu giao nhận chi tiết
    
    PENDING_WEIGHT1 --> CANCELLED : Nhân viên cân (Operator) hủy phiên cân
    PENDING_WEIGHT2 --> CANCELLED : Nhân viên cân (Operator) hủy phiên cân
    ALLOCATION_PENDING --> CANCELLED : Nhân viên cân (Operator) hủy phiên cân
    
    COMPLETED --> [*]
    CANCELLED --> [*] : Trả các đơn cắt lệnh (Cut Orders) về lại trạng thái chờ (`IN_YARD`)
"""
    try:
        b64 = encode_kroki(state_machine_code)
        url = f"https://kroki.io/mermaid/png/{b64}"
        print(f"Requesting Kroki URL: {url[:100]}...")
        
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
        
        req = urllib.request.Request(
            url, 
            headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'}
        )
        
        with urllib.request.urlopen(req, context=ctx, timeout=15) as response:
            img_data = response.read()
            with open("temp_state_machine.png", "wb") as f:
                f.write(img_data)
            print("Successfully downloaded Kroki Mermaid image!")
            return True
    except Exception as e:
        print(f"Error downloading from Kroki: {e}")
        return False

if __name__ == '__main__':
    test()
