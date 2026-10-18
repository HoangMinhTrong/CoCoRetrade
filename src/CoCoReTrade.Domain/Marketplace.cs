namespace CoCoReTrade.Domain;

public enum ProductStatus { PendingApproval, Active, Rejected, Reserved, Sold }
public enum OfferStatus { PendingSeller, PendingBuyer, Accepted, Declined, Expired, Used }
public enum OrderStatus { PendingProcessing, Shipping, ReadyForPickup, Completed, Cancelled }
public enum PaymentStatus { Unpaid, Paid }
public enum FulfillmentType { Pickup, Shipping }
public enum DeliveryWindow { Morning, Afternoon, BusinessHours }

public static class ShippingFeeRules
{
    public const decimal InnerCity = 20_000;
    public const decimal OuterCity = 35_000;
    public static decimal Calculate(FulfillmentType fulfillment, bool isInnerCity) => fulfillment switch
    {
        FulfillmentType.Pickup => 0,
        FulfillmentType.Shipping => isInnerCity ? InnerCity : OuterCity,
        _ => throw new ArgumentOutOfRangeException(nameof(fulfillment))
    };
}

public static class CommissionRules
{
    public static decimal Calculate(decimal merchandisePrice, int percent)
    {
        if (percent is < 15 or > 20) throw new ArgumentOutOfRangeException(nameof(percent));
        if (merchandisePrice <= 0 || merchandisePrice != decimal.Truncate(merchandisePrice))
            throw new ArgumentOutOfRangeException(nameof(merchandisePrice));
        return decimal.Round(merchandisePrice * percent / 100, 0, MidpointRounding.AwayFromZero);
    }
}

public sealed class Product
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string SellerId { get; private set; } = string.Empty;
    public string ConsignmentCode { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Brand { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Size { get; private set; } = string.Empty;
    public decimal OriginalPrice { get; private set; }
    public decimal AskingPrice { get; private set; }
    public int DeclaredCondition { get; private set; }
    public int? VerifiedCondition { get; private set; }
    public bool AllowOffers { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.PendingApproval;
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? CoverSelectedAtUtc { get; private set; }
    public DateTime? ReminderSentAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public List<ProductImage> Images { get; private set; } = [];
    public List<Inspection> Inspections { get; private set; } = [];
    public Guid? LatestInspectionId { get; private set; }
    public bool CanPublish => Status == ProductStatus.PendingApproval &&
        Inspections.Any(x => x.Id == LatestInspectionId && x.Approved);

    private Product() { }

    public static Product Submit(string sellerId, string consignmentCode, string name, string brand, string category, string size,
        decimal originalPrice, decimal askingPrice, int condition, bool allowOffers, IReadOnlyCollection<ProductImage> images)
    {
        if (string.IsNullOrWhiteSpace(sellerId) || string.IsNullOrWhiteSpace(consignmentCode)) throw new ArgumentException("Cần có người bán và mã ký gửi.");
        ValidateDetails(name, brand, category, size, originalPrice, askingPrice, condition);
        if (images.Count < 3) throw new ArgumentException("Cần ít nhất 3 ảnh sản phẩm.");
        return new Product { SellerId = sellerId, ConsignmentCode = consignmentCode, Name = name.Trim(), Brand = brand.Trim(), Category = category.Trim(),
            Size = size.Trim(), OriginalPrice = originalPrice, AskingPrice = askingPrice, DeclaredCondition = condition,
            AllowOffers = allowOffers, Images = images.ToList() };
    }

    public Inspection RecordInspection(string inspectorId, bool passed, string name, string brand, string category, string size,
        decimal originalPrice, decimal askingPrice, int condition, string? notes,
        IReadOnlyCollection<ProductImage> inspectionImages, DateTime nowUtc)
    {
        if (Status != ProductStatus.PendingApproval) throw new InvalidOperationException("Chỉ sản phẩm chờ duyệt mới được kiểm định.");
        if (string.IsNullOrWhiteSpace(inspectorId) || inspectorId == SellerId) throw new UnauthorizedAccessException("Không được tự kiểm định sản phẩm của mình.");
        ValidateDetails(name, brand, category, size, originalPrice, askingPrice, condition);
        if (notes?.Trim().Length > 2000)
            throw new ArgumentException("Ghi chú kiểm định không được vượt quá 2,000 ký tự.");
        if (inspectionImages.Count > 10) throw new ArgumentException("Chỉ được tải tối đa 10 ảnh kiểm định.");
        if (passed)
        {
            Name = name.Trim(); Brand = brand.Trim(); Category = category.Trim(); Size = size.Trim();
            OriginalPrice = originalPrice; AskingPrice = askingPrice; VerifiedCondition = condition;
        }
        var inspection = new Inspection(Id, inspectorId, passed, name.Trim(), brand.Trim(), category.Trim(), size.Trim(),
            originalPrice, askingPrice, condition, notes?.Trim() ?? string.Empty, nowUtc);
        foreach (var image in inspectionImages)
        {
            image.SetSortOrder(Images.Count);
            image.AssignInspection(inspection.Id);
            Images.Add(image);
            inspection.Images.Add(image);
        }
        Inspections.Add(inspection);
        LatestInspectionId = inspection.Id;
        return inspection;
    }

    public void Publish(string sku, DateTime nowUtc)
    {
        if (Status != ProductStatus.PendingApproval) throw new InvalidOperationException("Chỉ sản phẩm chờ duyệt mới được đăng bán.");
        if (!CanPublish)
            throw new InvalidOperationException("Cần kiểm định hiện vật đạt yêu cầu trước khi đăng bán.");
        if (string.IsNullOrWhiteSpace(Sku) && (string.IsNullOrWhiteSpace(sku) ||
            !System.Text.RegularExpressions.Regex.IsMatch(sku, @"^CC-\d{4}-\d{6}$")))
            throw new ArgumentException("Mã SKU sản phẩm không hợp lệ.", nameof(sku));
        if (!string.IsNullOrWhiteSpace(Sku))
            throw new InvalidOperationException("Sản phẩm đã có mã SKU chính thức.");
        Sku = sku;
        Status = ProductStatus.Active; PublishedAtUtc = nowUtc;
    }

    public void SelectCover(Guid imageId, string inspectorId, DateTime nowUtc)
    {
        if (Status != ProductStatus.PendingApproval || inspectorId == SellerId)
            throw new UnauthorizedAccessException("Ảnh bìa chỉ được chọn trong quá trình kiểm định.");
        var selected = Images.FirstOrDefault(x => x.Id == imageId) ??
            throw new KeyNotFoundException("Ảnh không thuộc sản phẩm này.");
        var ordered = Images.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        ordered.Remove(selected);
        ordered.Insert(0, selected);
        for (var index = 0; index < ordered.Count; index++) ordered[index].SetSortOrder(index);
        CoverSelectedAtUtc = nowUtc;
    }

    public void Reject(string reason)
    {
        if (Status != ProductStatus.PendingApproval) throw new InvalidOperationException("Chỉ sản phẩm chờ duyệt mới được từ chối.");
        if (Inspections.Count == 0) throw new InvalidOperationException("Hãy kiểm định hiện vật trước khi từ chối.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 1000)
            throw new ArgumentException("Lý do từ chối phải dài từ 5 đến 1,000 ký tự.");
        Status = ProductStatus.Rejected; RejectionReason = reason.Trim();
    }

    public void Reserve()
    {
        if (Status != ProductStatus.Active) throw new InvalidOperationException("Chỉ sản phẩm đang bán mới được giữ cho đơn hàng.");
        Status = ProductStatus.Reserved;
    }
    public void Restock()
    {
        if (Status != ProductStatus.Reserved) throw new InvalidOperationException("Chỉ sản phẩm đang được giữ mới được mở bán lại.");
        Status = ProductStatus.Active;
    }
    public void MarkSold()
    {
        if (Status != ProductStatus.Reserved) throw new InvalidOperationException("Chỉ sản phẩm đang được giữ mới được đánh dấu đã bán.");
        Status = ProductStatus.Sold;
    }

    private static void ValidateDetails(string name, string brand, string category, string size, decimal original, decimal asking, int condition)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(brand) ||
            string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(size) ||
            name.Trim().Length is < 3 or > 200 || brand.Trim().Length is < 2 or > 100 ||
            category.Trim().Length is < 2 or > 80 || size.Trim().Length is < 1 or > 30)
            throw new ArgumentException("Cần nhập tên, thương hiệu, danh mục và kích cỡ sản phẩm.");
        if (asking <= 0 || original < asking || asking > 999_999_999_999_999_999m ||
            original > 999_999_999_999_999_999m || asking != decimal.Truncate(asking) ||
            original != decimal.Truncate(original) || condition is < 50 or > 100)
            throw new ArgumentException("Giá hoặc độ mới của sản phẩm không hợp lệ.");
    }
}

public sealed class ProductImage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public Guid? InspectionId { get; private set; }
    private ProductImage() { }
    public ProductImage(string storageKey, string contentType, int sortOrder)
    { StorageKey = storageKey; ContentType = contentType; SortOrder = sortOrder; }
    public void AssignInspection(Guid inspectionId) => InspectionId = inspectionId;
    public void SetSortOrder(int order) => SortOrder = order;
}

public sealed class Inspection
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public string InspectorId { get; private set; } = string.Empty;
    public bool Approved { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Brand { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Size { get; private set; } = string.Empty;
    public decimal OriginalPrice { get; private set; }
    public decimal AskingPrice { get; private set; }
    public int VerifiedCondition { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public List<ProductImage> Images { get; private set; } = [];
    private Inspection() { }
    public Inspection(Guid productId, string inspectorId, bool approved, string name, string brand, string category,
        string size, decimal originalPrice, decimal askingPrice, int condition, string? notes, DateTime nowUtc)
    {
        ProductId = productId; InspectorId = inspectorId; Approved = approved;
        Name = name; Brand = brand; Category = category; Size = size;
        OriginalPrice = originalPrice; AskingPrice = askingPrice; VerifiedCondition = condition;
        Notes = notes?.Trim() ?? string.Empty; CreatedAtUtc = nowUtc;
    }
}

public sealed class Offer
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public string ProductSku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string BuyerId { get; private set; } = string.Empty;
    public string SellerId { get; private set; } = string.Empty;
    public decimal AskingPrice { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public decimal LastBuyerPrice { get; private set; }
    public decimal? LastSellerPrice { get; private set; }
    public decimal? AcceptedPrice { get; private set; }
    public OfferStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public List<OfferEvent> Events { get; private set; } = [];

    private Offer() { }
    public static Offer Create(Product product, string buyerId, decimal price, decimal minimumPrice, int usedBuyerOffers, DateTime nowUtc)
    {
        if (product.Status != ProductStatus.Active || !product.AllowOffers) throw new InvalidOperationException("Sản phẩm này không nhận đề xuất giá.");
        if (buyerId == product.SellerId) throw new UnauthorizedAccessException("Không thể trả giá sản phẩm của chính mình.");
        if (usedBuyerOffers >= 3) throw new InvalidOperationException("Bạn đã dùng hết 3 lượt trả giá cho sản phẩm này.");
        if (price < minimumPrice || price >= product.AskingPrice || price != decimal.Truncate(price))
            throw new ArgumentOutOfRangeException(nameof(price), "Giá đề xuất nằm ngoài khoảng cho phép.");
        var offer = new Offer { ProductId = product.Id, ProductSku = product.Sku, ProductName = product.Name,
            BuyerId = buyerId, SellerId = product.SellerId, AskingPrice = product.AskingPrice, CurrentPrice = price,
            LastBuyerPrice = price, Status = OfferStatus.PendingSeller, CreatedAtUtc = nowUtc, ExpiresAtUtc = nowUtc.AddHours(24) };
        offer.Events.Add(new OfferEvent(offer.Id, buyerId, OfferEventAction.Submitted, price, nowUtc));
        return offer;
    }
    public void Accept(string actorId, ProductStatus productStatus, DateTime nowUtc)
    {
        EnsureAction(actorId, productStatus, nowUtc);
        AcceptedPrice = CurrentPrice; Status = OfferStatus.Accepted; ExpiresAtUtc = nowUtc.AddHours(24);
        Events.Add(new OfferEvent(Id, actorId, OfferEventAction.Accepted, CurrentPrice, nowUtc));
    }
    public void Decline(string actorId, ProductStatus productStatus, DateTime nowUtc)
    {
        EnsureAction(actorId, productStatus, nowUtc);
        Status = OfferStatus.Declined;
        Events.Add(new OfferEvent(Id, actorId, OfferEventAction.Declined, null, nowUtc));
    }
    public void Counter(string actorId, decimal price, decimal minimumPrice, int usedBuyerOffers, ProductStatus productStatus, DateTime nowUtc)
    {
        EnsureAction(actorId, productStatus, nowUtc);
        if (price != decimal.Truncate(price)) throw new ArgumentOutOfRangeException(nameof(price), "Giá phải là số nguyên VNĐ.");
        if (Status == OfferStatus.PendingBuyer && usedBuyerOffers >= 3)
            throw new InvalidOperationException("Người mua đã dùng hết 3 lượt trả giá cho sản phẩm này.");
        var range = CounterPriceRange(minimumPrice, usedBuyerOffers);
        if (range is null)
            throw new InvalidOperationException("Không còn khoảng giá hợp lệ để đề xuất lại. Bạn có thể chấp nhận hoặc từ chối mức giá hiện tại.");
        if (price < range.Value.Minimum || price > range.Value.Maximum)
            throw new ArgumentOutOfRangeException(nameof(price),
                $"Giá đề xuất lại phải từ {range.Value.Minimum:N0} VNĐ đến {range.Value.Maximum:N0} VNĐ.");
        if (Status == OfferStatus.PendingSeller)
        {
            LastSellerPrice = price; Status = OfferStatus.PendingBuyer;
            Events.Add(new OfferEvent(Id, actorId, OfferEventAction.SellerCountered, price, nowUtc));
        }
        else
        {
            LastBuyerPrice = price; Status = OfferStatus.PendingSeller;
            Events.Add(new OfferEvent(Id, actorId, OfferEventAction.BuyerCountered, price, nowUtc));
        }
        CurrentPrice = price; ExpiresAtUtc = nowUtc.AddHours(24);
    }
    public (decimal Minimum, decimal Maximum)? CounterPriceRange(decimal minimumPrice, int usedBuyerOffers)
    {
        decimal minimum;
        decimal maximum;
        if (Status == OfferStatus.PendingSeller)
        {
            minimum = LastBuyerPrice + 1;
            maximum = LastSellerPrice is null ? AskingPrice : Math.Min(AskingPrice, LastSellerPrice.Value - 1);
        }
        else if (Status == OfferStatus.PendingBuyer && usedBuyerOffers < 3 && LastSellerPrice is not null)
        {
            minimum = Math.Max(minimumPrice, LastBuyerPrice + 1);
            maximum = LastSellerPrice.Value - 1;
        }
        else return null;
        return minimum <= maximum ? (minimum, maximum) : null;
    }
    public void UseForOrder(string buyerId, ProductStatus productStatus, DateTime nowUtc)
    {
        if (buyerId != BuyerId) throw new UnauthorizedAccessException("Giá đã chấp nhận thuộc về người mua khác.");
        if (productStatus != ProductStatus.Active || Status != OfferStatus.Accepted || ExpiresAtUtc <= nowUtc || AcceptedPrice is null)
            throw new InvalidOperationException("Giá thỏa thuận không còn hiệu lực hoặc đã hết hạn.");
        Status = OfferStatus.Used;
        Events.Add(new OfferEvent(Id, buyerId, OfferEventAction.Ordered, AcceptedPrice, nowUtc));
    }
    public OfferStatus EffectiveStatus(DateTime nowUtc) =>
        (Status is OfferStatus.PendingSeller or OfferStatus.PendingBuyer or OfferStatus.Accepted) && ExpiresAtUtc <= nowUtc
            ? OfferStatus.Expired : Status;
    public bool Expire(DateTime nowUtc)
    {
        if (EffectiveStatus(nowUtc) != OfferStatus.Expired || Status == OfferStatus.Expired) return false;
        Status = OfferStatus.Expired;
        Events.Add(new OfferEvent(Id, "system", OfferEventAction.Expired, null, nowUtc));
        return true;
    }
    private void EnsureAction(string actorId, ProductStatus productStatus, DateTime nowUtc)
    {
        if (productStatus != ProductStatus.Active || ExpiresAtUtc <= nowUtc) throw new InvalidOperationException("Đề xuất giá hoặc sản phẩm không còn hiệu lực.");
        var expected = Status switch { OfferStatus.PendingSeller => SellerId, OfferStatus.PendingBuyer => BuyerId, _ => null };
        if (expected is null) throw new InvalidOperationException("Đề xuất giá này không chờ phản hồi.");
        if (actorId != expected) throw new UnauthorizedAccessException("Chỉ người nhận đề xuất mới được phản hồi.");
    }
}
public enum OfferEventAction { Submitted, SellerCountered, BuyerCountered, Accepted, Declined, Ordered, Expired }
public sealed class OfferEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OfferId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public OfferEventAction Action { get; private set; }
    public decimal? Price { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    private OfferEvent() { }
    public OfferEvent(Guid offerId, string actorId, OfferEventAction action, decimal? price, DateTime nowUtc)
    { OfferId = offerId; ActorId = actorId; Action = action; Price = price; CreatedAtUtc = nowUtc; }
}
public sealed class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public Guid? OfferId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string BuyerId { get; private set; } = string.Empty;
    public string SellerId { get; private set; } = string.Empty;
    public string ProductSku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string ProductSize { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string RecipientPhone { get; private set; } = string.Empty;
    public string? Province { get; private set; }
    public string? Ward { get; private set; }
    public string? StreetAddress { get; private set; }
    public DeliveryWindow? PreferredDeliveryWindow { get; private set; }
    public string? DeliveryNote { get; private set; }
    public string? PickupAddress { get; private set; }
    public DateTime? PickupAtUtc { get; private set; }
    public DateTime? PickupDeadlineAtUtc { get; private set; }
    public decimal ItemAmount { get; private set; }
    public decimal ShippingFee { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Unpaid;
    public string? PaymentReference { get; private set; }
    public string? PaymentConfirmedById { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public FulfillmentType Fulfillment { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Order() { }
    public static Order Create(Product product, Offer? offer, string buyerId, Guid idempotencyKey,
        FulfillmentType fulfillment, string recipientName, string recipientPhone, string? province,
        string? ward, string? streetAddress, DateTime? pickupAtUtc, string? pickupAddress,
        decimal shippingFee, int commissionPercent, DateTime nowUtc,
        DeliveryWindow? preferredDeliveryWindow = null, string? deliveryNote = null)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("Thiếu mã xác nhận yêu cầu đặt hàng.");
        if (product.SellerId == buyerId) throw new UnauthorizedAccessException("Không thể mua sản phẩm của chính mình.");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("Sản phẩm không còn mở bán.");
        if (offer is not null && (offer.BuyerId != buyerId || offer.ProductId != product.Id))
            throw new UnauthorizedAccessException("Giá đã chấp nhận thuộc về người mua khác.");
        if (offer is not null && (offer.Status != OfferStatus.Accepted || offer.ExpiresAtUtc <= nowUtc || offer.AcceptedPrice is null))
            throw new InvalidOperationException("Giá thỏa thuận không áp dụng cho người mua này.");
        if (string.IsNullOrWhiteSpace(recipientName) || recipientName.Trim().Length < 2 ||
            string.IsNullOrWhiteSpace(recipientPhone) || !System.Text.RegularExpressions.Regex.IsMatch(recipientPhone, @"^0[35789][0-9]{8}$"))
            throw new ArgumentException("Tên hoặc số điện thoại người nhận không hợp lệ.");
        if (fulfillment == FulfillmentType.Pickup)
        {
            if (preferredDeliveryWindow.HasValue || !string.IsNullOrWhiteSpace(deliveryNote))
                throw new ArgumentException("Nhận tại cửa hàng không dùng thông tin giao tận nơi.");
            if (pickupAtUtc is null || pickupAtUtc.Value.Kind != DateTimeKind.Utc || pickupAtUtc <= nowUtc ||
                string.IsNullOrWhiteSpace(pickupAddress) || pickupAddress.Trim().Length > 300)
                throw new ArgumentException("Cần chọn thời gian nhận hàng trong tương lai.");
            if (shippingFee != 0) throw new ArgumentException("Nhận tại cửa hàng không tính phí giao hàng.");
            if (offer is not null && pickupAtUtc >= offer.ExpiresAtUtc)
                throw new ArgumentException("Đơn theo giá thỏa thuận phải được nhận trong 24 giờ từ lúc chấp nhận giá.");
        }
        else if (fulfillment == FulfillmentType.Shipping)
        {
            if (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(ward) || string.IsNullOrWhiteSpace(streetAddress) ||
                province.Trim().Length is < 2 or > 120 || ward.Trim().Length is < 2 or > 120 ||
                streetAddress.Trim().Length is < 4 or > 300)
                throw new ArgumentException("Giao tận nơi cần tỉnh/thành, phường/xã và địa chỉ cụ thể.");
            if (shippingFee != ShippingFeeRules.InnerCity && shippingFee != ShippingFeeRules.OuterCity)
                throw new ArgumentException("Phí giao hàng không hợp lệ.");
            if (pickupAtUtc is not null) throw new ArgumentException("Giao tận nơi không dùng giờ nhận tại cửa hàng.");
            if (preferredDeliveryWindow.HasValue && !Enum.IsDefined(preferredDeliveryWindow.Value))
                throw new ArgumentException("Khung giờ giao hàng không hợp lệ.");
            if (deliveryNote?.Length > 500) throw new ArgumentException("Ghi chú giao hàng quá dài.");
        }
        else throw new ArgumentOutOfRangeException(nameof(fulfillment));
        if (commissionPercent is < 15 or > 20) throw new ArgumentOutOfRangeException(nameof(commissionPercent));
        var item = offer?.AcceptedPrice ?? product.AskingPrice;
        var commission = CommissionRules.Calculate(item, commissionPercent);
        return new Order { ProductId = product.Id, OfferId = offer?.Id, IdempotencyKey = idempotencyKey,
            BuyerId = buyerId, SellerId = product.SellerId, ProductSku = product.Sku, ProductName = product.Name, ProductSize = product.Size,
            RecipientName = recipientName.Trim(), RecipientPhone = recipientPhone,
            Province = fulfillment == FulfillmentType.Shipping ? province!.Trim() : null,
            Ward = fulfillment == FulfillmentType.Shipping ? ward!.Trim() : null,
            StreetAddress = fulfillment == FulfillmentType.Shipping ? streetAddress!.Trim() : null,
            PreferredDeliveryWindow = fulfillment == FulfillmentType.Shipping ? preferredDeliveryWindow : null,
            DeliveryNote = fulfillment == FulfillmentType.Shipping ? deliveryNote?.Trim() : null,
            PickupAtUtc = fulfillment == FulfillmentType.Pickup ? pickupAtUtc : null,
            PickupDeadlineAtUtc = fulfillment == FulfillmentType.Pickup ? offer?.ExpiresAtUtc : null,
            PickupAddress = fulfillment == FulfillmentType.Pickup ? pickupAddress!.Trim() : null,
            Fulfillment = fulfillment, ShippingFee = shippingFee, ItemAmount = item, CommissionAmount = commission,
            TotalAmount = item + shippingFee, Status = OrderStatus.PendingProcessing, CreatedAtUtc = nowUtc };
    }
    public bool MatchesSubmission(Guid productId, Guid? offerId, FulfillmentType fulfillment, string name,
        string phone, string? province, string? ward, string? street, DateTime? pickupAtUtc,
        DeliveryWindow? preferredDeliveryWindow = null, string? deliveryNote = null) =>
        ProductId == productId && OfferId == offerId && Fulfillment == fulfillment &&
        RecipientName == name.Trim() && RecipientPhone == phone &&
        Province == (fulfillment == FulfillmentType.Shipping ? province?.Trim() : null) &&
        Ward == (fulfillment == FulfillmentType.Shipping ? ward?.Trim() : null) &&
        StreetAddress == (fulfillment == FulfillmentType.Shipping ? street?.Trim() : null) &&
        PickupAtUtc == (fulfillment == FulfillmentType.Pickup ? pickupAtUtc : null) &&
        PreferredDeliveryWindow == (fulfillment == FulfillmentType.Shipping ? preferredDeliveryWindow : null) &&
        DeliveryNote == (fulfillment == FulfillmentType.Shipping ? deliveryNote?.Trim() : null);
    public void MarkReady()
    {
        if (Status != OrderStatus.PendingProcessing) throw new InvalidOperationException("Chỉ đơn mới đang chờ xử lý mới được chuẩn bị.");
        Status = Fulfillment == FulfillmentType.Pickup ? OrderStatus.ReadyForPickup : OrderStatus.Shipping;
    }
    public void ConfirmPayment(string adminId, string reference, DateTime nowUtc)
    {
        if (Status is not (OrderStatus.ReadyForPickup or OrderStatus.Shipping) || PaymentStatus != PaymentStatus.Unpaid ||
            PickupDeadlineAtUtc.HasValue && nowUtc >= PickupDeadlineAtUtc.Value)
            throw new InvalidOperationException("Không thể xác nhận thanh toán cho đơn này.");
        if (string.IsNullOrWhiteSpace(adminId) || string.IsNullOrWhiteSpace(reference) ||
            reference.Trim().Length is < 4 or > 100)
            throw new ArgumentException("Cần có mã đối soát thanh toán.");
        PaymentStatus = PaymentStatus.Paid; PaymentReference = reference.Trim(); PaymentConfirmedById = adminId; PaidAtUtc = nowUtc;
    }
    public void Complete(Product product, DateTime nowUtc)
    {
        if (ProductId != product.Id || product.Status != ProductStatus.Reserved ||
            Status is not (OrderStatus.ReadyForPickup or OrderStatus.Shipping) || PaymentStatus != PaymentStatus.Paid)
            throw new InvalidOperationException("Cần chuẩn bị đơn và xác nhận thanh toán trước khi hoàn tất.");
        product.MarkSold(); Status = OrderStatus.Completed; CompletedAtUtc = nowUtc;
    }
    public void Cancel(Product product, bool isAdmin, DateTime nowUtc)
    {
        if (ProductId != product.Id || product.Status != ProductStatus.Reserved || PaymentStatus != PaymentStatus.Unpaid ||
            Status != OrderStatus.PendingProcessing && (!isAdmin || Status != OrderStatus.ReadyForPickup))
            throw new InvalidOperationException("Chỉ đơn chưa thanh toán và chưa giao đi mới được hủy.");
        product.Restock(); Status = OrderStatus.Cancelled; CancelledAtUtc = nowUtc;
    }
    public void ReturnUndelivered(Product product, DateTime nowUtc)
    {
        if (ProductId != product.Id || product.Status != ProductStatus.Reserved ||
            Status != OrderStatus.Shipping || PaymentStatus != PaymentStatus.Unpaid)
            throw new InvalidOperationException("Chỉ đơn chưa thanh toán, hàng đã trả về CoCo mới được mở bán lại.");
        product.Restock(); Status = OrderStatus.Cancelled; CancelledAtUtc = nowUtc;
    }
}
public sealed class Favorite
{
    public string UserId { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
public sealed class SellerFollow
{
    public string FollowerId { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
public sealed class Wallet
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public void Credit(decimal amount)
    {
        if (amount <= 0 || amount != decimal.Truncate(amount) ||
            amount > 999_999_999_999_999_999m - Balance) throw new ArgumentOutOfRangeException(nameof(amount));
        Balance += amount;
    }
    public void ReserveWithdrawal(decimal amount)
    {
        if (amount <= 0 || amount != decimal.Truncate(amount) || amount > Balance)
            throw new ArgumentOutOfRangeException(nameof(amount), "Số dư khả dụng không đủ.");
        Balance -= amount;
    }
}
public sealed class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? WithdrawalRequestId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
public sealed class WithdrawalRequest
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public Guid RequestKey { get; set; }
    // Retained for withdrawals created before full bank details were collected.
    public string BankAccountMasked { get; set; } = string.Empty;
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string Status { get; private set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
    public string? DecidedById { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public string? DecisionReason { get; private set; }
    public string? TransferredById { get; private set; }
    public DateTime? TransferredAtUtc { get; private set; }
    public string? TransferReference { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Approve(string adminId, DateTime nowUtc)
    {
        if (Status != "Pending") throw new InvalidOperationException("Yêu cầu rút tiền đã được xử lý.");
        if (string.IsNullOrWhiteSpace(BankAccountNumber) || string.IsNullOrWhiteSpace(BankName))
            throw new InvalidOperationException("Yêu cầu cũ thiếu thông tin ngân hàng. Hãy từ chối để người bán gửi yêu cầu mới.");
        Status = "Approved"; DecidedById = adminId; DecidedAtUtc = nowUtc;
    }
    public void Reject(string adminId, string reason, DateTime nowUtc)
    {
        if (Status is not ("Pending" or "Approved"))
            throw new InvalidOperationException("Chỉ yêu cầu chưa chuyển tiền mới được từ chối.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 3 or > 1000)
            throw new ArgumentException("Cần nhập lý do từ chối.");
        Status = "Rejected"; DecidedById = adminId; DecidedAtUtc = nowUtc; DecisionReason = reason.Trim();
    }
    public void ConfirmTransferred(string adminId, string reference, DateTime nowUtc)
    {
        if (Status != "Approved")
            throw new InvalidOperationException("Chỉ yêu cầu đã duyệt mới được xác nhận đã chuyển tiền.");
        if (string.IsNullOrWhiteSpace(BankAccountNumber) || string.IsNullOrWhiteSpace(BankName))
            throw new InvalidOperationException("Yêu cầu cũ thiếu thông tin ngân hàng. Hãy từ chối để người bán gửi yêu cầu mới.");
        if (string.IsNullOrWhiteSpace(adminId) || string.IsNullOrWhiteSpace(reference) ||
            reference.Trim().Length is < 4 or > 100)
            throw new ArgumentException("Mã giao dịch chuyển khoản phải dài từ 4 đến 100 ký tự.");
        Status = "Transferred"; TransferredById = adminId; TransferredAtUtc = nowUtc;
        TransferReference = reference.Trim();
    }
}
public sealed class MarketplaceNotification
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? OfferId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? ProductId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
public sealed class CartItem
{
    public string UserId { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BuyerId { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastMessageAtUtc { get; set; }
    public List<ConversationMessage> Messages { get; set; } = [];
}
public sealed class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
public enum SupportTicketStatus { Open, Closed }
public sealed class SupportTicket
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserId { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public SupportTicketStatus Status { get; private set; } = SupportTicketStatus.Open;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastActivityAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public List<SupportMessage> Messages { get; private set; } = [];
    private SupportTicket() { }
    public static SupportTicket Create(string userId, string subject, string body, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(subject) ||
            subject.Trim().Length is < 5 or > 150)
            throw new ArgumentException("Chủ đề phải có từ 5 đến 150 ký tự.");
        var ticket = new SupportTicket { UserId = userId, Subject = subject.Trim(),
            CreatedAtUtc = nowUtc, LastActivityAtUtc = nowUtc };
        ticket.Messages.Add(SupportMessage.Create(ticket.Id, userId, body, nowUtc));
        return ticket;
    }
    public SupportMessage Reply(string actorId, string body, DateTime nowUtc)
    {
        if (Status != SupportTicketStatus.Open) throw new InvalidOperationException("Yêu cầu đã đóng.");
        var message = SupportMessage.Create(Id, actorId, body, nowUtc);
        Messages.Add(message); LastActivityAtUtc = nowUtc;
        return message;
    }
    public void Close(DateTime nowUtc)
    {
        if (Status != SupportTicketStatus.Open) throw new InvalidOperationException("Yêu cầu đã đóng.");
        Status = SupportTicketStatus.Closed; ClosedAtUtc = nowUtc; LastActivityAtUtc = nowUtc;
    }
}
public sealed class SupportMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TicketId { get; private set; }
    public string SenderId { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    private SupportMessage() { }
    public static SupportMessage Create(Guid ticketId, string senderId, string body, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(body) ||
            body.Trim().Length is < 3 or > 2000)
            throw new ArgumentException("Nội dung phải có từ 3 đến 2000 ký tự.");
        return new SupportMessage { TicketId = ticketId, SenderId = senderId,
            Body = body.Trim(), CreatedAtUtc = nowUtc };
    }
}
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
}
public sealed class Address
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
}
public sealed class ShippingZoneMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Province { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public bool IsInnerCity { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class AuditLog
{
    public Guid Id { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
