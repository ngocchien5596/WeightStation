import os
import re

srs_path = r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md'

with open(srs_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Define headers to partition the document
headers = [
    r"# 1\. Giới thiệu \(Introduction\)",
    r"# 2\. Mô tả Tổng quan \(Overall Description\)",
    r"# 3\. Yêu cầu Chi tiết \(Specific Requirements\)",
    r"# 4\. Thiết kế Cơ sở dữ liệu Cục bộ \(Local Database Design\)",
    r"# 5\. Các Phụ lục \(Appendices\)"
]

# Find split indices
indices = []
for header in headers:
    match = re.search(header, content)
    if match:
        indices.append((match.start(), match.end()))
    else:
        print(f"Header not found: {header}")

if len(indices) == 5:
    part0 = content[:indices[0][0]] # document metadata and history
    part1 = content[indices[0][0]:indices[1][0]] # Chapter 1: Introduction
    part2 = content[indices[1][0]:indices[2][0]] # Chapter 2: Overall Description
    part3 = content[indices[2][0]:indices[3][0]] # Chapter 3: Specific Requirements
    part4 = content[indices[3][0]:indices[4][0]] # Chapter 4: Database Design
    part5 = content[indices[4][0]:] # Chapter 5: Appendices

    # Process and restructure headings in Database Design (to become Chapter 2)
    # Target: Change "# 4. Thiết kế..." to "# 2. Thiết kế..."
    # Target: Change "## 4.1 Sơ đồ..." to "## 2.1 Sơ đồ..."
    # Target: Change "## 4.2 Chi tiết..." to "## 2.2 Chi tiết..."
    # Target: Change "### 4.2.x Bảng..." to "### 2.2.x Bảng..."
    # Target: Change "Bảng 4.2.x:" to "Bảng 2.2.x:"
    part4_mod = part4.replace("# 4. Thiết kế Cơ sở dữ liệu Cục bộ", "# 2. Thiết kế Cơ sở dữ liệu Cục bộ")
    part4_mod = part4_mod.replace("## 4.1 Sơ đồ Mối quan hệ Thực thể", "## 2.1 Sơ đồ Mối quan hệ Thực thể")
    part4_mod = part4_mod.replace("## 4.2 Chi tiết Lược đồ các Bảng Cơ sở dữ liệu", "## 2.2 Chi tiết Lược đồ các Bảng Cơ sở dữ liệu")
    part4_mod = re.sub(r"### 4\.2\.(\d+)", r"### 2.2.\1", part4_mod)
    part4_mod = re.sub(r"Bảng 4\.2\.(\d+)", r"Bảng 2.2.\1", part4_mod)

    # Process and restructure headings in Overall Description (to become Chapter 3)
    # Target: Change "# 2. Mô tả Tổng quan" to "# 3. Mô tả Tổng quan"
    # Target: Change "## 2.x" to "## 3.x"
    # Target: Change "### 2.x.y" to "### 3.x.y"
    part2_mod = part2.replace("# 2. Mô tả Tổng quan (Overall Description)", "# 3. Mô tả Tổng quan (Overall Description)")
    # Replace sub-headings: ## 2.1 to ## 2.5
    part2_mod = part2_mod.replace("## 2.1 Bối cảnh Sản phẩm", "## 3.1 Bối cảnh Sản phẩm")
    part2_mod = part2_mod.replace("### 2.1.1 Bối cảnh Hệ thống", "### 3.1.1 Bối cảnh Hệ thống")
    part2_mod = part2_mod.replace("### 2.1.2 Giao diện Hệ thống", "### 3.1.2 Giao diện Hệ thống")
    part2_mod = part2_mod.replace("### 2.1.3 Giao diện Người dùng", "### 3.1.3 Giao diện Người dùng")
    part2_mod = part2_mod.replace("### 2.1.4 Giao diện Phần cứng", "### 3.1.4 Giao diện Phần cứng")
    part2_mod = part2_mod.replace("### 2.1.5 Giao diện Phần mềm", "### 3.1.5 Giao diện Phần mềm")
    part2_mod = part2_mod.replace("### 2.1.6 Giao diện Mạng và Truyền thông", "### 3.1.6 Giao diện Mạng và Truyền thông")
    part2_mod = part2_mod.replace("## 2.2 Các Chức năng chính của Sản phẩm", "## 3.2 Các Chức năng chính của Sản phẩm")
    part2_mod = part2_mod.replace("## 2.3 Đặc điểm Người dùng", "## 3.3 Đặc điểm Người dùng")
    part2_mod = part2_mod.replace("## 2.4 Ràng buộc Hệ thống", "## 3.4 Ràng buộc Hệ thống")
    part2_mod = part2_mod.replace("## 2.5 Giả định và Phụ thuộc", "## 3.5 Giả định và Phụ thuộc")

    # Process and restructure headings in Specific Requirements (to become Chapter 4)
    # Target: Change "# 3. Yêu cầu Chi tiết" to "# 4. Yêu cầu Chi tiết"
    # Target: Change "## 3.x" to "## 4.x"
    # Target: Change "### 3.x.y" to "### 4.x.y"
    part3_mod = part3.replace("# 3. Yêu cầu Chi tiết (Specific Requirements)", "# 4. Yêu cầu Chi tiết (Specific Requirements)")
    part3_mod = part3_mod.replace("## 3.1 Yêu cầu Giao diện bên ngoài", "## 4.1 Yêu cầu Giao diện bên ngoài")
    part3_mod = part3_mod.replace("### 3.1.1 Giao diện Người dùng", "### 4.1.1 Giao diện Người dùng")
    part3_mod = part3_mod.replace("### 3.1.2 Tích hợp Cân điện tử", "### 4.1.2 Tích hợp Cân điện tử")
    part3_mod = part3_mod.replace("### 3.1.3 Tích hợp Camera Giám sát", "### 4.1.3 Tích hợp Camera Giám sát")
    part3_mod = part3_mod.replace("## 3.2 Yêu cầu Chức năng", "## 4.2 Yêu cầu Chức năng")
    part3_mod = part3_mod.replace("### 3.2.1 Xác thực và Phân quyền", "### 4.2.1 Xác thực và Phân quyền")
    part3_mod = part3_mod.replace("### 3.2.2 Quy trình Vận hành Cân Nội địa Tiêu chuẩn", "### 4.2.2 Quy trình Vận hành Cân Nội địa Tiêu chuẩn")
    part3_mod = part3_mod.replace("### 3.2.3 Phân bổ Khối lượng Thực tế", "### 4.2.3 Phân bổ Khối lượng Thực tế")
    part3_mod = part3_mod.replace("### 3.2.4 In ấn Chứng từ và Xe ra", "### 4.2.4 In ấn Chứng từ và Xe ra")
    part3_mod = part3_mod.replace("### 3.2.5 Quy trình Cân Xuất khẩu Đơn hàng Lớn", "### 4.2.5 Quy trình Cân Xuất khẩu Đơn hàng Lớn")
    part3_mod = part3_mod.replace("### 3.2.6 Quy trình CO Lại Cắt Lệnh và Kế Thừa Lượt Cân", "### 4.2.6 Quy trình CO Lại Cắt Lệnh và Kế Thừa Lượt Cân")
    part3_mod = part3_mod.replace("### 3.2.7 Tự động và Thủ công Sao lưu dữ liệu local", "### 4.2.7 Tự động và Thủ công Sao lưu dữ liệu local")
    part3_mod = part3_mod.replace("### 3.2.8 Giải quyết Quá tải", "### 4.2.8 Giải quyết Quá tải")
    part3_mod = part3_mod.replace("### 3.2.9 Đồng bộ dữ liệu", "### 4.2.9 Đồng bộ dữ liệu")
    part3_mod = part3_mod.replace("## 3.3 Yêu cầu Phi Chức năng", "## 4.3 Yêu cầu Phi Chức năng")
    part3_mod = part3_mod.replace("### 3.3.1 Yêu cầu về Hiệu năng", "### 4.3.1 Yêu cầu về Hiệu năng")
    part3_mod = part3_mod.replace("### 3.3.2 Yêu cầu về Bảo mật", "### 4.3.2 Yêu cầu về Bảo mật")
    part3_mod = part3_mod.replace("### 3.3.3 Yêu cầu về Độ tin cậy và Khả năng chịu lỗi", "### 4.3.3 Yêu cầu về Độ tin cậy và Khả năng chịu lỗi")
    part3_mod = part3_mod.replace("### 3.3.4 Yêu cầu về Tính Dễ sử dụng", "### 4.3.4 Yêu cầu về Tính Dễ sử dụng")

    # Update references in Overview (Part 1 / Section 1.5)
    # Old overview references:
    # - **Phần 2**: Mô tả tổng quan...
    # - **Phần 3**: Đặc tả chi tiết...
    # - **Các Phụ lục**...
    # New overview:
    # - **Phần 2**: Thiết kế cơ sở dữ liệu cục bộ...
    # - **Phần 3**: Mô tả tổng quan...
    # - **Phần 4**: Đặc tả chi tiết...
    # - **Chương 5**: Các Phụ lục...
    part1_mod = part1
    old_overview = """- **Phần 2**: Mô tả tổng quan về bối cảnh, kiến trúc giao tiếp phần cứng, các nhóm người dùng và các ràng buộc hệ thống.
- **Phần 3**: Đặc tả chi tiết các yêu cầu chức năng (được đánh mã FR-XXX) và các yêu cầu phi chức năng (được đánh mã NFR-XXX) bám sát mã nguồn hiện tại.
- **Các Phụ lục**: Lược đồ cơ sở dữ liệu chi tiết, các sơ đồ nghiệp vụ và ma trận truy xuất nguồn gốc yêu cầu."""

    new_overview = """- **Phần 2**: Thiết kế Cơ sở dữ liệu Cục bộ (Local Database Design) đặc tả sơ đồ thực thể ERD và cấu hình chi tiết các bảng.
- **Phần 3**: Mô tả Tổng quan (Overall Description) về bối cảnh, mô hình hoạt động, đặc điểm người dùng và các ràng buộc hệ thống.
- **Phần 4**: Đặc tả chi tiết các yêu cầu chức năng (mã FR-XXX) và phi chức năng (mã NFR-XXX) bám sát nghiệp vụ và mã nguồn.
- **Phần 5**: Các Phụ lục (Máy trạng thái, Ma trận truy xuất yêu cầu, và Bàn giao phê duyệt)."""

    part1_mod = part1_mod.replace(old_overview, new_overview)

    # Reassemble in the requested order:
    # Metadata -> Chapter 1: Introduction -> Chapter 2: Database Design -> Chapter 3: Overall Description -> Chapter 4: Specific Requirements -> Chapter 5: Appendices
    new_content = part0 + part1_mod + part4_mod + part2_mod + part3_mod + part5

    with open(srs_path, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("SRS successfully restructured: Database Design is now Chapter 2!")
else:
    print("Could not partition the document. Please verify the headers.")
