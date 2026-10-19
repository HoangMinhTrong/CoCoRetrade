-- One-time repair for the three known demo products in the local CoCoReTrade database.
-- The values 5, 9, and 15 were entered as if the form used thousands of VND.
-- Run only against the development database after reviewing the identifiers below.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @reservedProduct uniqueidentifier = '5FD0E841-4E15-4A93-933E-722F54B912F2';
    DECLARE @offer uniqueidentifier = '5C5DEBBA-D472-4A96-B93D-9D5148BAA35E';
    DECLARE @order uniqueidentifier = '5ADD16C1-7130-4CE1-9A4E-6FE8D2BC8C66';

    IF (SELECT COUNT(*) FROM Products WHERE AskingPrice < 1000) <> 3
        THROW 51000, 'Unexpected number of low-priced demo products; no data changed.', 1;
    IF NOT EXISTS (SELECT 1 FROM Products WHERE ConsignmentCode = 'CG-2026-000001' AND OriginalPrice = 22 AND AskingPrice = 15)
        OR NOT EXISTS (SELECT 1 FROM Products WHERE ConsignmentCode = 'CG-2026-000004' AND Id = @reservedProduct AND OriginalPrice = 12 AND AskingPrice = 9 AND Status = 'Reserved')
        OR NOT EXISTS (SELECT 1 FROM Products WHERE ConsignmentCode = 'CG-2026-000009' AND OriginalPrice = 11 AND AskingPrice = 5)
        THROW 51001, 'Demo product values changed; no data changed.', 1;
    IF (SELECT COUNT(*) FROM Inspections WHERE AskingPrice < 1000) <> 2
        OR (SELECT COUNT(*) FROM Offers WHERE AskingPrice < 1000) <> 1
        OR (SELECT COUNT(*) FROM Orders WHERE ItemAmount < 1000) <> 1
        THROW 51002, 'Related demo data differs from the reviewed snapshot; no data changed.', 1;
    IF NOT EXISTS (SELECT 1 FROM Offers WHERE Id = @offer AND ProductId = @reservedProduct
                   AND AskingPrice = 9 AND CurrentPrice = 9 AND LastBuyerPrice = 8
                   AND LastSellerPrice = 9 AND AcceptedPrice = 9 AND Status = 'Used')
        OR NOT EXISTS (SELECT 1 FROM Orders WHERE Id = @order AND ProductId = @reservedProduct
                       AND OfferId = @offer AND ItemAmount = 9 AND ShippingFee = 35000
                       AND TotalAmount = 35009 AND CommissionAmount = 1
                       AND PaymentStatus = 'Unpaid')
        OR EXISTS (SELECT 1 FROM WalletTransactions WHERE OrderId = @order)
        THROW 51003, 'The order is paid, credited, or otherwise changed; no data changed.', 1;

    UPDATE Products
       SET OriginalPrice = OriginalPrice * 1000, AskingPrice = AskingPrice * 1000
     WHERE ConsignmentCode IN ('CG-2026-000001', 'CG-2026-000004', 'CG-2026-000009');
    IF @@ROWCOUNT <> 3 THROW 51004, 'Product repair count mismatch.', 1;

    UPDATE i
       SET OriginalPrice = i.OriginalPrice * 1000, AskingPrice = i.AskingPrice * 1000
      FROM Inspections AS i
      JOIN Products AS p ON p.Id = i.ProductId
     WHERE p.ConsignmentCode IN ('CG-2026-000004', 'CG-2026-000009');
    IF @@ROWCOUNT <> 2 THROW 51005, 'Inspection repair count mismatch.', 1;

    UPDATE Offers
       SET AskingPrice = AskingPrice * 1000, CurrentPrice = CurrentPrice * 1000,
           LastBuyerPrice = LastBuyerPrice * 1000,
           LastSellerPrice = LastSellerPrice * 1000,
           AcceptedPrice = AcceptedPrice * 1000
     WHERE Id = @offer;
    IF @@ROWCOUNT <> 1 THROW 51006, 'Offer repair count mismatch.', 1;

    UPDATE OfferEvents SET Price = Price * 1000 WHERE OfferId = @offer AND Price IS NOT NULL;
    IF @@ROWCOUNT <> 4 THROW 51007, 'Offer event repair count mismatch.', 1;

    UPDATE Orders
       SET ItemAmount = ItemAmount * 1000,
           CommissionAmount = ROUND(ItemAmount * 1000 * 0.15, 0),
           TotalAmount = ShippingFee + ItemAmount * 1000
     WHERE Id = @order;
    IF @@ROWCOUNT <> 1 THROW 51008, 'Order repair count mismatch.', 1;

    COMMIT TRANSACTION;
    PRINT 'Corrected three demo products, two inspections, one offer, four offer events, and one unpaid order.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
