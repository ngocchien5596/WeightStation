namespace StationApp.UI.Resources;

public static class UiText
{
    public static class Common
    {
        public const string NoMatchingData = "Không tìm thấy dữ liệu phù hợp.";
        public const string SearchIncomingLoadError = "Không thể tải danh sách xe vào. Vui lòng thử lại.";
        public const string SearchOutgoingLoadError = "Không thể tải danh sách xe ra. Vui lòng thử lại.";
        public const string RequiredVehiclePlate = "Vui lòng nhập Số PTVC.";
        public const string RequiredDriverName = "Vui lòng nhập Tên tài xế.";
        public const string RequiredTtcp = "Vui lòng nhập TTCP.";
        public const string RequiredCustomer = "Vui lòng nhập Khách hàng.";
        public const string RequiredProductCode = "Vui lòng nhập Mã sản phẩm.";
        public const string RequiredProductName = "Vui lòng nhập sản phẩm.";
        public const string RequiredPlannedWeight = "Vui lòng nhập SL đặt.";
        public const string No = "Không";
        public const string Confirm = "Xác nhận";
    }

    public static class Incoming
    {
        public const string CreateInboundError = "Không thể tạo xe nhập hàng.";
        public const string UpdateSelectionRequired = "Vui lòng chọn xe cần cập nhật.";
        public const string UpdateInboundError = "Không thể cập nhật thông tin xe vào.";
        public const string SaveInboundError = "Không thể lưu thông tin xe vào. Vui lòng thử lại.";
        public const string CreateSessionSelectionRequired = "Vui lòng chọn ít nhất một xe để tạo lượt cân.";
        public const string CreateSessionSuccess = "Đã tạo lượt cân và chuyển sang màn Lập phiếu cân.";
        public const string DetailLoadError = "Không thể tải thông tin chi tiết xe vào.";
        public const string CreateInboundSuccess = "Đã tạo xe nhập hàng thành công.";
        public const string UpdateInboundSuccess = "Đã lưu thay đổi thông tin xe nhập hàng.";
    }

    public static class Diagnostics
    {
        public const string WaitingData = "(chờ dữ liệu...)";
        public const string CheckingConnection = "Đang kiểm tra...";
        public const string ActiveConnection = "Đang hoạt động";
        public const string LostConnection = "Mất kết nối";
        public const string CentralApiNotConfigured = "(chưa cấu hình)";
        public const string MasterDataNotSynced = "(chưa đồng bộ)";
    }

    public static class Weighing
    {
        public const string InitializingDevice = "Đang khởi tạo...";
        public const string ManualModeForbidden = "Tài khoản hiện tại không có quyền cân tay.";
        public const string LoadSessionsError = "Không thể tải danh sách lượt cân.";
        public const string InvalidWeight1 = "Trọng lượng không hợp lệ để lưu cân lần 1.";
        public const string InvalidWeight2 = "Trọng lượng không hợp lệ để lưu cân lần 2.";
        public const string Weight1Saved = "Đã lưu cân lần 1.";
        public const string Weight2Saved = "Đã lưu cân lần 2.";
        public const string AllocationSaved = "Đã phân bổ thực giao thành công.";
        public const string CancelTitle = "Hủy lượt cân";
        public const string CancelMessage = "Bạn có chắc muốn hủy lượt cân này không?";
        public const string CancelConfirm = "Hủy session";
        public const string Close = "Đóng";
        public const string CancelSuccess = "Đã hủy lượt cân.";
        public const string WeighTicketDisplay = "phiếu cân";
        public const string DeliveryTicketDisplay = "phiếu giao nhận";
        public const string NoPrintableDocumentFormat = "Chưa có {0} để in.";
        public const string PrintDialogWeighTicket = "In phiếu cân tổng";
        public const string PrintDialogDeliveryTicket = "In phiếu giao nhận";
        public const string PrintErrorFormat = "Không thể in {0}.";
        public const string CompleteSuccess = "Session đã hoàn tất và chuyển sang danh sách xe ra.";
        public const string PrintSuccessFormat = "Đã in {0} thành công.";
        public const string PrintPreviewWeigh = "In phiếu cân";
        public const string PrintPreviewWeighMaster = "In phiếu cân tổng";
        public const string PrintPreviewDelivery = "In phiếu giao nhận";
        public const string RelatedWeighTicket = "PHIẾU CÂN";
        public const string RelatedDeliveryTicket = "PHIẾU GIAO NHẬN";
        public const string ActiveConnection = "Đang hoạt động";
        public const string LostConnection = "Mất kết nối";
        public const string MoveOutConfirmTitle = "Xác nhận chuyển xe ra";
        public const string MoveOutConfirmMessage = "Bạn có chắc muốn chuyển lượt xe này sang Danh sách xe ra không?";
        public const string MoveOutConfirmAction = "Chuyển xe ra";
        public const string MoveOutSuccess = "Đã chuyển lượt xe sang Danh sách xe ra.";
        public const string MoveOutError = "Không thể chuyển lượt xe ra. Vui lòng thử lại.";
        public const string MoveOutNotReady = "Lượt xe chưa đủ điều kiện để chuyển ra.";
    }

    public static class Startup
    {
        public const string UiExceptionTitle = "Lỗi Hệ Thống";
        public const string UiExceptionFormat = "Lỗi giao diện nghiêm trọng: {0}";
        public const string FatalExceptionFormat = "Lỗi hệ thống nghiêm trọng: {0}";
        public const string StartupChecksHeader = "Startup checks phát hiện lỗi nghiêm trọng:\n\n";
        public const string StartupChecksFooter = "\nỨng dụng có thể không hoạt động đúng.";
        public const string StartupChecksTitle = "Station App - Cảnh báo khởi động";
    }
}
