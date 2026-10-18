using CoCoReTrade.Domain;

namespace CoCoReTrade.Web.Models;

// Translate values at the presentation boundary; persisted codes and API contracts stay stable.
public static class UiText
{
    public static string AuditDetails(string? value) => For(value)
        .Replace("TrustScore=", "Điểm tín nhiệm=", StringComparison.Ordinal)
        .Replace("suspended=True", "tạm khóa=có", StringComparison.Ordinal)
        .Replace("suspended=False", "tạm khóa=không", StringComparison.Ordinal)
        .Replace("reason=", "lý do=", StringComparison.Ordinal)
        .Replace("Withdrawal=", "Yêu cầu rút tiền=", StringComparison.Ordinal);

    public static string For(object? value) => value switch
    {
        ProductStatus.PendingApproval => "Chờ kiểm định",
        ProductStatus.Active => "Đang bán",
        ProductStatus.Rejected => "Bị từ chối",
        ProductStatus.Reserved => "Đã có người đặt",
        ProductStatus.Sold => "Đã bán",
        OfferStatus.PendingSeller => "Chờ người bán phản hồi",
        OfferStatus.PendingBuyer => "Chờ người mua phản hồi",
        OfferStatus.Accepted => "Đã chấp nhận",
        OfferStatus.Declined => "Đã từ chối",
        OfferStatus.Expired => "Đã hết hạn",
        OfferStatus.Used => "Đã đặt hàng",
        OfferEventAction.Submitted => "Gửi giá đề xuất",
        OfferEventAction.SellerCountered => "Người bán đề xuất lại",
        OfferEventAction.BuyerCountered => "Người mua đề xuất lại",
        OfferEventAction.Accepted => "Chấp nhận giá",
        OfferEventAction.Declined => "Từ chối giá",
        OfferEventAction.Ordered => "Đã đặt hàng",
        OfferEventAction.Expired => "Hết hạn",
        OrderStatus.PendingProcessing => "Chờ xử lý",
        OrderStatus.Shipping => "Đang giao hàng",
        OrderStatus.ReadyForPickup => "Sẵn sàng nhận tại CoCo",
        OrderStatus.Completed => "Hoàn tất",
        OrderStatus.Cancelled => "Đã hủy",
        PaymentStatus.Unpaid => "Chưa thanh toán",
        PaymentStatus.Paid => "Đã thanh toán",
        FulfillmentType.Pickup => "Nhận tại CoCo",
        FulfillmentType.Shipping => "Giao tận nơi",
        DeliveryWindow.Morning => "Buổi sáng",
        DeliveryWindow.Afternoon => "Buổi chiều",
        DeliveryWindow.BusinessHours => "Giờ hành chính",
        string text => text switch
        {
            "Pending" => "Chờ duyệt",
            "Approved" => "Đã duyệt, chờ chuyển tiền",
            "Rejected" => "Đã từ chối",
            "Transferred" => "Đã chuyển tiền",
            "Transactions" => "Giao dịch và trả giá",
            "Listings" => "Sản phẩm",
            "Social" => "Tương tác",
            "System" => "Hệ thống",
            "offers" or "Offers" => "Đề xuất giá",
            "orders" or "Orders" => "Đơn hàng",
            "Wallets" or "wallets" => "Ví ký gửi",
            "Messages" or "messages" => "Tin nhắn",
            "Support" or "support" => "Hỗ trợ",
            "Admin" => "Quản trị viên",
            "Inspector" => "Kiểm định viên",
            "User" => "Người dùng",
            "SellerPayout" => "Ghi có tiền bán hàng",
            "WithdrawalReserved" => "Giữ tiền cho yêu cầu rút",
            "WithdrawalReleased" => "Hoàn tiền yêu cầu rút",
            "WithdrawalSettled" => "Xác nhận đã chuyển tiền",
            "Products.Inspected" => "Đã kiểm định sản phẩm",
            "Products.CoverSelected" => "Đã chọn ảnh bìa",
            "Products.Published" => "Đã đăng bán sản phẩm",
            "Products.Rejected" => "Đã từ chối sản phẩm",
            "OrderPlaced" => "Đã tạo đơn hàng",
            "OrderCancelled" => "Đã hủy đơn hàng",
            "OrderPrepared" => "Đã chuẩn bị đơn hàng",
            "OrderCompleted" => "Đã hoàn tất đơn hàng",
            "PaymentConfirmed" => "Đã xác nhận thanh toán",
            "DeliveryReturnedUndelivered" => "Hàng không được nhận và đã trả về",
            "Wallet.SellerPayout" => "Đã ghi có tiền bán hàng",
            "Wallet.WithdrawalRequested" => "Đã yêu cầu rút tiền",
            "Wallet.WithdrawalApproved" => "Đã duyệt yêu cầu rút tiền",
            "Wallet.WithdrawalRejected" => "Đã từ chối yêu cầu rút tiền",
            "Wallet.WithdrawalTransferred" => "Đã xác nhận chuyển tiền",
            "Users.InspectorGranted" => "Đã cấp quyền kiểm định",
            "Users.InspectorRevoked" => "Đã thu hồi quyền kiểm định",
            "Users.PublishGranted" => "Đã cấp quyền đăng bán",
            "Users.PublishRevoked" => "Đã thu hồi quyền đăng bán",
            "Users.Locked" => "Đã khóa tài khoản",
            "Users.Unlocked" => "Đã mở khóa tài khoản",
            "Users.TrustScoreChanged" => "Đã cập nhật điểm tín nhiệm",
            "Support.Replied" => "Đã trả lời yêu cầu hỗ trợ",
            "Support.Closed" => "Đã đóng yêu cầu hỗ trợ",
            "Products.ReadPending" => "Xem sản phẩm chờ kiểm định",
            "Products.Publish" => "Quyền đăng bán sản phẩm",
            "Products.Inspect" => "Quyền kiểm định sản phẩm",
            "Products.Reject" => "Quyền từ chối sản phẩm",
            "Orders.Manage" => "Quản lý đơn hàng",
            "Wallets.Manage" => "Quản lý ví",
            "Free size" => "Kích cỡ tự do",
            _ => text
        },
        null => "",
        _ => value.ToString() ?? ""
    };
}
