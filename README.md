# CoCo ReTrade

ASP.NET Core MVC marketplace for inspected secondhand clothing. The solution uses .NET 10, SQL Server, ASP.NET Core Identity, EF Core, Bootstrap 5, and xUnit.

## Projects

| Project | Responsibility |
|---|---|
| `src/CoCoReTrade.Domain` | Product lifecycle and future marketplace entities |
| `src/CoCoReTrade.Application` | DTOs, service contracts, validation and product use cases |
| `src/CoCoReTrade.Infrastructure` | SQL Server, Identity, private image storage, email delivery and seeding |
| `src/CoCoReTrade.Web` | MVC, Razor views, `/Admin` and `/api/v1` |
| `tests/CoCoReTrade.Tests` | xUnit business and visibility rules |

Dependency direction: Application → Domain; Infrastructure → Application/Domain; Web → Application/Infrastructure.

## Requirements

- .NET 10 SDK (`dotnet --version`)
- Microsoft SQL Server with permission to create the `CoCoReTrade` database
- SMTP credentials for activation/reset email outside Development

This workspace also contains a local SDK at `.dotnet/dotnet.exe` (ignored by Git). Use that executable instead of `dotnet` if the SDK is not installed system wide.

## Configure

From the repository root:

```powershell
dotnet user-secrets set "ConnectionStrings:CoCo" "Server=localhost;Database=CoCoReTrade;Trusted_Connection=True;TrustServerCertificate=True" --project src/CoCoReTrade.Web
dotnet user-secrets set "Admin:InitialPassword" "<choose-a-strong-temporary-password>" --project src/CoCoReTrade.Web
dotnet user-secrets set "Shipping:StoreAddress" "<actual CoCo store address>" --project src/CoCoReTrade.Web
```

Alternatively, set `COCO_ADMIN_INITIAL_PASSWORD` as an environment variable. The app requires it **only when creating the initial admin**. Do not commit this password. Identity hashes it through `UserManager`; the admin must change it after first login.

The default admin account is `admin@cocoretrade.vn` / `0905123456`. The user is seeded on the first run after migrations. The password is never stored in source code or migration files.

An optional second admin uses the display name `admin1` and signs in with `admin1@cocoretrade.vn`. To create it in another database, set `COCO_ADMIN1_INITIAL_PASSWORD` in the app process before its first startup, then remove the variable. The seeder hashes the password through Identity, assigns User/Inspector/Admin roles, and requires a password change at first login. It never promotes an existing non-admin account with that email.

For a deployed environment, set `Storage:Root`, `Smtp:Host`, `Smtp:Port`, `Smtp:From`, `Smtp:User`, and `Smtp:Password` using environment variables or a secret manager. See `appsettings.example.json`. In Development, six-digit activation and password reset codes are shown on the confirmation page and logged. In other environments, SMTP is required. Never use the Development code display in production.

## Restore, migrate, run

To review the UI before installing SQL Server, run this in PowerShell. This Development-only mode uses six illustrated sample products and does not write data. Login, registration, listing submission and checkout require SQL Server and are disabled in this mode.

```powershell
$env:Preview__Enabled = "true"
dotnet run --project src/CoCoReTrade.Web --launch-profile http
```

Open `http://localhost:5001/preview` to browse the example screens. Remove the variable with `Remove-Item Env:Preview__Enabled` before running against SQL Server.

```powershell
dotnet restore CoCoReTrade.slnx
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/CoCoReTrade.Infrastructure --startup-project src/CoCoReTrade.Web
dotnet run --project src/CoCoReTrade.Web
```

The application also applies pending migrations at startup. To create later migrations:

```powershell
dotnet tool run dotnet-ef migrations add NameOfChange --project src/CoCoReTrade.Infrastructure --startup-project src/CoCoReTrade.Web --output-dir Migrations
dotnet tool run dotnet-ef database update --project src/CoCoReTrade.Infrastructure --startup-project src/CoCoReTrade.Web
```

The migrations are checked in. SQL Server's `dbo.ProductSkuSequence` allocates unique numeric suffixes for both consignment codes (`CG-YYYY-XXXXXX`) and inventory SKUs (`CC-YYYY-XXXXXX`). A SKU is assigned only after a passed physical inspection. Both columns have unique indexes; an empty SKU is allowed only while a listing is unpublished. VND amounts use `decimal(18,0)`. Product, offer, order and wallet rows have SQL Server row-version columns.

If EF reports a connection or encryption error, first compare the `Server=` value in `ConnectionStrings:CoCo` with **Server Name** in your working SSMS connection. The design-time factory now reads Web user secrets and `ConnectionStrings__CoCo`; it does not need a password in source code. Verify the SQL Server service and local TLS/certificate settings before applying migrations. A failed connection check does not apply the new migrations.

## Phase 1 flows

1. Register with name, unique email and phone, then enter the six-digit email code within five minutes. Resetting a password uses a separate one-time code, revokes prior sessions, and signs in a new session.
2. Sign in with email or phone. The same account may list products and browse the shop.
3. Upload 3–10 JPG/PNG/WebP photos (maximum 5 MB each) and submit a listing. Photos live outside `wwwroot` and are served only when the viewer has access.
4. The seller sees the consignment code and `PendingApproval` status in **Tủ đồ của tôi**. Public home, shop, search, details and image requests reveal only `Active` products.
5. An Inspector opens `/Admin`, checks the physical item, uploads optional inspection photos and records the verified condition, brand, size, price and notes. A passed inspection assigns an inventory SKU but leaves the listing pending.
6. A user with explicit `Products.Publish` permission confirms publication. An Admin may reject an inspected listing with a reason. Both actions are audited and notify the seller.

## Admin and Inspector

The `User`, `Inspector` and `Admin` roles are seeded at startup. Inspector accounts retain User capabilities and receive `Products.ReadPending` and `Products.Inspect`. Admin receives all Inspector permissions plus product publication, rejection, product/order/user/wallet management and audit access. `Products.Publish` is an explicit role claim for Admin; an Admin can also grant that single permission to an Inspector from `/Admin/Users`. Role or permission changes invalidate the affected user's sign-in cookie, so they must sign in again.

The `/Admin` dashboard links to pending consignments, product management, orders, users, wallet reconciliation and audit history. A printable SKU label includes a locally rendered QR code. The wallet screen lists completed, paid orders awaiting seller proceeds. An Admin explicitly confirms a release to credit `merchandise - commission` once, excluding shipping; the action is audited and notifies the seller. Users may request withdrawals from available funds, and Admin may approve or reject them. Approval records a decision; it does not confirm a bank transfer. Automatic payment and bank-transfer integrations are not configured.

`InspectorWorkflow` migrates existing submissions: their old SKU becomes the consignment code, while pending and rejected products receive an empty inventory SKU until a passed inspection. Apply it with the `dotnet-ef database update` command above before using the new Admin screens. Back up a database containing real consignments before applying this migration.

Offers and counter-offers are implemented. A buyer can send at most three prices per product, including counter-offers, within the configured discount floor. The receiving party may accept, decline or counter within 24 hours. Every submitted price and response is stored in `OfferEvents` and sends an in-app notification to the other party. An accepted price belongs only to that buyer for 24 hours. It does not reserve the product.

Checkout accepts Buy Now at the current listing price or the current buyer's accepted, unexpired offer. Pickup is free and requires a future appointment within the next 1–3 days during configured store hours. Delivery requires province/city, ward/commune and street address. The backend computes shipping and the final total. Order creation reserves one physical product in a SQL transaction, using its row-version plus a unique active-order index. A per-buyer idempotency key returns the same order on a repeated identical request. The order snapshots product, agreed price, commission, recipient and pickup/delivery address. Admin and seller receive in-app order notifications.

An order starts `PendingProcessing` and `Unpaid`. A buyer may cancel only while it is unpaid and pending; an admin may also cancel an unpaid pickup order that is ready. Cancellation returns the product to `Active`. An admin may mark a pickup order ready or a delivery order shipping, manually confirm actual payment with a reference, then complete it. Completion requires payment confirmation and changes the product to `Sold`. These actions write audit records. No payment gateway or automatic wallet credit is implemented. Seller order views omit the buyer's contact and address details.

Favorites and addresses remain database foundations for later phases. Wallet credit is an explicit Admin reconciliation action after a paid order is completed. Withdrawal requests use a masked bank-account reference and an idempotency key; no bank payout flow is implemented.

`Marketplace:MaximumOfferDiscountPercent` defaults to `25` and must be within `20–30`; `Marketplace:CommissionPercent` defaults to `15` and must be within `15–20`. `Shipping:InnerCityZones` contains exact current province/city and ward/commune pairs, for example `[{ "Province": "<current province>", "Ward": "<current ward>" }]`. These pairs seed the `ShippingZones` table only when it is empty; after that, maintain the rows in SQL Server (`Province`, `Ward`, `IsInnerCity`, `IsActive`). No district list is assumed. An active inner-city pair costs 20,000 VND; all other valid addresses cost 35,000 VND. Pickup costs zero and requires `Shipping:StoreAddress` to be set. Configure `Shipping:StoreUtcOffsetHours`, `OpeningHour` and `ClosingHour` for the store. Invalid discount/commission or shipping-hour configuration prevents startup.

The `CheckoutOrders` migration preserves legacy combined order addresses in `StreetAddress`. Existing address-book district text is appended to its street address; it is not silently treated as a ward. Users should update ward/commune details when those address-book entries are used in a later phase.

## System services

Apply migration `SystemServices` with the `dotnet-ef database update` command above, or restart the web app after backing up a database with real orders and wallets. The application applies pending migrations on startup. Consignment codes and inventory SKUs use the SQL Server sequence and unique indexes. Product and order state changes stay in application/domain services; background workers never publish products.

Four scoped background workers run only in normal database mode: offer expiry (every minute), unpaid-order expiry (every minute), notification outbox dispatch (every 15 seconds), and a one-time reminder for active listings older than `Listings:ReminderAfterDays` (every six hours). `Orders:UnpaidReservationMinutes` defaults to 1440 and accepts 15 through 10080. Only unpaid, still-pending orders are automatically cancelled and restocked. A placed order remains unpaid; completion still requires explicit payment confirmation.

In-app notifications are saved with email and web-push outbox records in the same SQL transaction. Configure `Smtp:Host`, `Smtp:From` and optional SMTP credentials to enable email delivery. No web-push provider is registered yet; those outbox records remain deferred. Delivery is retryable and at-least-once, so a provider should use the outbox message ID to deduplicate sends. Neither bank transfers nor web-push delivery should be treated as completed by this code.

## Added marketplace screens

- Buyers can like active products, follow sellers, view saved items and use a per-account cart. Cart items are limited to 20 and do not reserve stock; checkout still creates one order for one unique item.
- Buyers may open a private conversation with the seller of an active item. Buyer and seller can exchange in-app messages after that; other users cannot read them. The other party receives a persisted notification. The conversation page requires refresh to see new messages; no live transport is configured.
- The launch profiles open `/Splash`, which shows a loading skeleton and routes an existing Identity session to Home or a guest to Login. Public Home and Shop still list active products when visited directly.
- The profile Help link opens a SQL-backed support inbox. A user can create and reply to their own request; an Admin with `ManageUsers` permission can reply and close it. Replies notify the other side, and Admin actions are audited.
- Profile includes avatar upload, a trust score, address book, wallet ledger and withdrawal requests. Admin can change trust scores with an audit reason; accounts below the configured threshold are suspended. Admins can choose a product cover image during inspection.
- Notifications have transaction, listing and social groups with a read action. A background worker reminds sellers once when an active listing ages past `Listings:ReminderAfterDays` (7–365, default 30); it never changes prices or publishes items.
- Checkout can select a saved address, store a delivery window and delivery note in the order snapshot, and show a store map link when `Shipping:StoreMapUrl` is configured to an HTTPS URL. Pickup stays unavailable until the real `Shipping:StoreAddress` is set.
- `ExperienceCompletion`, `ShoppingCart`, `MessagingAndCoverVersion` and `CustomerSupport` are new migrations. Back up a real database, then run the migration command above or restart the app when SQL Server is available. The EF design-time factory reads the same connection string from Web user secrets or `ConnectionStrings__CoCo`.

This workspace has no actual CoCo store address or external provider account configured. Email OTP works in Development with the displayed code; production needs SMTP. SMS OTP, Google/Apple sign-in, browser push, card payments and bank transfers need provider credentials and deployment setup. No successful delivery or bank transfer is inferred from a local notification or an approved withdrawal.

## Versioned API

- `GET /api/v1/products` and `GET /api/v1/products/{id}` — public, active products only
- `GET /api/v1/listings/mine`, `POST /api/v1/listings` — authenticated user
- `GET /api/v1/admin/inspections/pending`, `GET /api/v1/admin/inspections/{id}` — Inspector or Admin
- `POST /api/v1/admin/inspections/{id}` (multipart form with row version and optional images) — Inspector or Admin
- `POST /api/v1/admin/inspections/{id}/publish` — explicit `Products.Publish` permission; `POST .../{id}/reject` — Admin
- `GET /api/v1/admin/products`, `/dashboard`, `/audit`; `GET /api/v1/admin/users`, `POST .../inspector`, `/publish-grant`, `/lock`; `GET /api/v1/admin/wallets` — permission gated
- `POST /api/v1/products/{productId}/offers` — submit a price (`{ "price": 230000 }`)
- `GET /api/v1/offers`, `GET /api/v1/offers/{id}` — participant offer history
- `POST /api/v1/offers/{id}/accept`, `/decline`, `/counter` — receiving party; counter body is `{ "price": 250000 }`
- `GET /api/v1/orders/quote?productId=...&offerId=...&fulfillment=Shipping&province=...&ward=...` — server-calculated quote; omit `offerId` for Buy Now
- `POST /api/v1/orders` — place order with `productId`, optional `offerId`, required `idempotencyKey` UUID, recipient, fulfillment, and pickup local time (`yyyy-MM-ddTHH:mm`) or province/ward/street address
- `GET /api/v1/orders`, `GET /api/v1/orders/{id}`, `POST /api/v1/orders/{id}/cancel` — buyer/seller history and permitted cancellation
- `GET /api/v1/admin/orders`, `GET /api/v1/admin/orders/{id}`, `POST .../{id}/ready`, `/confirm-payment`, `/complete`, `/cancel` — Admin only
- `GET /api/v1/notifications` — the current user's notifications
- `GET /api/v1/notifications?group=transactions|listings|social`, `POST /api/v1/notifications/{id}/read` — grouped inbox and read action
- `GET /api/v1/social/favorites`, `PUT`/`DELETE /api/v1/social/favorites/{productId}`, `GET /api/v1/social/following`, `PUT`/`DELETE /api/v1/social/following/{sellerId}` — saved items and seller follows
- `GET /api/v1/cart`, `PUT`/`DELETE /api/v1/cart/items/{productId}` — current user's cart, active products only
- `GET /api/v1/messages`, `POST /api/v1/messages/products/{productId}`, `GET /api/v1/messages/{id}`, `POST /api/v1/messages/{id}/messages` — participant-only conversations
- `GET`/`POST /api/v1/support`, `GET /api/v1/support/{id}`, `POST /api/v1/support/{id}/replies`; Admin queue, reply and close actions live under `/api/v1/admin/support`
- `GET /api/v1/addresses`, `POST /api/v1/addresses`, `DELETE /api/v1/addresses/{id}` — current user's address book
- `GET /api/v1/wallet`, `POST /api/v1/wallet/withdrawals` — available/pending balance and idempotent withdrawal request (`requestKey`, `amount`, `bankAccountMasked`)
- `GET /api/v1/admin/wallets/withdrawals`, `POST /api/v1/admin/wallets/withdrawals/{id}/approve` or `/reject` — permission-gated decisions; rejection body is `{ "reason": "..." }`

For cookie-authenticated `POST` requests, first call `GET /api/v1/session/antiforgery`, retain the cookie and send the returned token in the `X-CSRF-TOKEN` header. MVC forms include anti-forgery tokens automatically.

## Tests

```powershell
dotnet test CoCoReTrade.slnx
```

The existing tests cover minimum photo count, publishing state transitions, privacy of pending listings/photos, offer authorization and expiry, Buy Now and negotiated prices, shipping zones, validation, idempotency, concurrent buyers, cancellation/restocking and paid completion. The new profile, cart, messaging and support flows still need dedicated automated tests. A live SQL Server is required to exercise the database migrations and full application; the existing unit tests do not require one.
#   C o C o R e t r a d e  
 