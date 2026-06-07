import base64
import urllib.request
import ssl

def test():
    mermaid_code = """erDiagram
    cut_orders ||--o| weighing_sessions : "WeighingSessionId"
    weighing_sessions ||--o{ weighing_session_lines : "WeighingSessionId"
    cut_orders ||--o{ weighing_session_lines : "CutOrderId"
    weighing_sessions ||--o{ weigh_tickets : "WeighingSessionId"
    weighing_session_lines ||--o| delivery_tickets : "DeliveryTicketId"
    
    vehicles ||--o{ weighing_sessions : "VehiclePlate"
    customers ||--o{ cut_orders : "CustomerCode"
    products ||--o{ cut_orders : "ProductCode"
    
    users ||--o{ audit_logs : "Actor"
"""
    try:
        # Encode to base64
        b64 = base64.b64encode(mermaid_code.encode('utf-8')).decode('utf-8')
        url = f"https://mermaid.ink/img/{b64}"
        print(f"Requesting URL: {url[:100]}...")
        
        # Bypass SSL certificate check if needed (local environments sometimes have issues)
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
        
        req = urllib.request.Request(
            url, 
            headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'}
        )
        
        with urllib.request.urlopen(req, context=ctx, timeout=15) as response:
            img_data = response.read()
            with open("temp_erd.png", "wb") as f:
                f.write(img_data)
            print("Successfully downloaded Mermaid image!")
            return True
    except Exception as e:
        print(f"Error downloading: {e}")
        return False

if __name__ == '__main__':
    test()
