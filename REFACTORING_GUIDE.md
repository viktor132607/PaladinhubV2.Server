# PaladinHub Controller Refactoring Guide

This file is the reference pattern for future controller refactors in `PaladinHubV2.Server`.

## Core refactoring rules

1. **Controllers own HTTP only**
   - routes and route compatibility
   - authentication/authorization boundary
   - model-state validation
   - HTTP status mapping
   - response shape

2. **Domain services own business logic**
   - business decisions and workflow
   - database reads/writes
   - external provider orchestration
   - cross-service coordination

3. **State/infrastructure concerns get their own service**
   - session serialization/deserialization
   - filesystem operations
   - state normalization
   - state reset

4. **Preserve public contracts while refactoring**
   - keep existing route URLs unless a separate migration is requested
   - keep response semantics compatible with the current client
   - split implementation without forcing a frontend rewrite

5. **Register every new service through DI**
   - interface + implementation
   - scoped lifetime by default when DB/session/request state is involved
   - register in application startup (`AddPaladinHubApp` or `Program.cs`)

6. **Split controllers by responsibility after business logic is extracted**
   - do not split first and duplicate the same business logic across controllers

7. **Never change working behavior during a refactor unless a functional change is explicitly requested**
   - if existing V2 behavior is known to work, preserve it exactly even if it looks unusual, inefficient, misleadingly named or inconsistent with common conventions
   - when restoring or comparing functionality with a working V1 implementation, treat the working V1 behavior as the source of truth unless explicitly instructed otherwise
   - do not "fix", reinterpret, modernize or simplify business rules, thresholds, ranges, calculations, ordering, defaults, status codes, routes, response shapes or side effects based only on developer judgment
   - a refactor is a structural/code-quality change, not permission to alter product behavior
   - functional changes and bug fixes require either an explicit request or confirmed evidence that the existing behavior is actually broken
   - if behavior looks suspicious but its intent is uncertain, preserve it and flag it separately instead of silently changing it

---

## Source-of-truth example 1: Checkout

The original `CheckoutController` mixed HTTP concerns with checkout state management, cart synchronization, wallet charging, Stripe calls, transaction persistence, order finalization and response routing.

### Controllers

- `CheckoutController`
  - Start
  - Shipping GET/POST
  - Payment GET/POST
  - Review

- `CheckoutOrdersController`
  - PlaceOrder
  - Registered
  - Success
  - Failure

- `CheckoutCardController`
  - Card
  - Card/Finalize

### Services

- `ICheckoutStateService` / `CheckoutStateService`
  - checkout session state
  - shipping normalization
  - state save/clear

- `ICheckoutService` / `CheckoutService`
  - checkout workflow orchestration
  - cart snapshot and checkout validation
  - review data
  - selects the appropriate order/payment service

- `ICheckoutOrderService` / `CheckoutOrderService`
  - wallet checkout
  - cash-on-delivery checkout
  - purchase transaction persistence
  - order metadata
  - cart archive/clear after completed order

- `ICheckoutPaymentService` / `CheckoutPaymentService`
  - Stripe PaymentIntent creation
  - Stripe payment verification
  - amount/currency/order/user verification

---

## Source-of-truth example 2: Account

The original `AccountController` mixed account overview queries, transaction pagination, wallet top-up, promo redemption, checkout discount session state, avatar filesystem operations, profile/security mutation, logout and placeholder settings/connections endpoints.

### Controllers

- `AccountController`
  - MyAccount
  - Overview

- `AccountRewardsController`
  - RedeemCode
  - DevTopUp

- `AccountAvatarController`
  - UploadAvatar
  - SetUploadedAvatar
  - DeleteUpload (POST and DELETE compatibility routes)
  - SetDefaultAvatar

- `AccountProfileController`
  - Settings
  - AccountDetails
  - Privacy
  - MarkPhoneVerified
  - profile/address placeholder endpoints

- `AccountConnectionsController`
  - Connections
  - ConnectProvider
  - RemoveApp

- `AccountSessionController`
  - Logout

All controllers keep the shared legacy/API route bases:

- `/Account/...`
- `/api/account/...`

### Services

- `IAccountOverviewService` / `AccountOverviewService`
  - account dashboard model composition
  - recent transactions
  - overview transaction pagination
  - wallet balance
  - security score/tips
  - uploaded-avatar list

- `IAccountRewardsService` / `AccountRewardsService`
  - promo redemption orchestration
  - promo failure classification
  - checkout discount session state
  - developer wallet top-up orchestration

- `IAvatarService` / `AvatarService`
  - upload filesystem operations
  - owned-upload path validation
  - selecting uploaded/default avatars
  - deleting uploads
  - clearing active avatar when its uploaded file is deleted

- `ISecurityService` / `SecurityService`
  - current-session logout
  - phone verification mutation
  - existing 2FA/recovery/logout-all functionality

- `IAccountUiService` / `AccountUiService`
  - current-user lookup
  - security-score calculation
  - region/currency compatibility helpers
  - uploaded-avatar enumeration

### Account-specific rule learned

Before creating a new service, inspect existing domain services. The Account refactor reused and extended `IAvatarService`, `ISecurityService`, `IWalletService`, `IPromoCodeService` and `IAccountUiService` instead of copying their responsibilities into new services.

---

## Source-of-truth example 3: Products

The original `ProductsController` mixed public catalog routing, category lookup, product details, Admin create/edit/delete flows, form-model preparation, category normalization and user review operations.

### Controllers

- `ProductsController`
  - merchandise redirect
  - categories
  - product details

- `ProductAdminController`
  - create model
  - create API + legacy route
  - edit model
  - update API + legacy route
  - delete API + legacy compatibility route

- `ProductReviewsController`
  - add review API + legacy route
  - delete review API + legacy route

All controllers preserve the shared route bases:

- `/Products/...`
- `/api/products/...`

### Services

- `IProductAdminService` / `ProductAdminService`
  - create/edit form-model preparation
  - category select-list composition
  - new-category normalization
  - create/update/delete orchestration through the existing product domain service

- `IProductReviewService` / `ProductReviewService`
  - review creation/deletion orchestration
  - keeps review business operations out of the HTTP controller

- `IProductService` / `ProductService`
  - remains the existing core product data/domain service
  - catalog queries and product details
  - persistent product CRUD implementation
  - review persistence used by the focused review service
  - cart-facing product queries used elsewhere in the application

### Product-specific rule learned

If a large controller already delegates most persistence/business rules to an existing domain service, do not duplicate that data-access logic into new services just to make new classes. Extract only the remaining orchestration/model-composition responsibilities and split the HTTP endpoints by domain boundary. A later dedicated `ProductService` refactor can split its own large responsibilities without coupling that work to the controller migration.

---

## Source-of-truth example 4: Carts

The original `CartsController` mixed user cart reads, anonymous/session cart mutations, Redis/session-to-persistent synchronization, cart response calculations and Admin archive access.

### Controllers

- `CartsController`
  - MyCart
  - Mini
  - CountJson

- `CartItemsController`
  - add item API
  - legacy AddProduct route
  - Increase
  - Decrease
  - RemoveProduct
  - Cancel

- `AdminCartsController`
  - archive list
  - archived-cart details

All controllers preserve the shared route bases and compatibility route:

- `/Cart/...`
- `/api/cart/...`
- `/Carts/AddProduct/{id}`

### Services

- `ICartApplicationService` / `CartApplicationService`
  - validates product IDs and quantity bounds
  - orchestrates add/increase/decrease/remove operations through `ICartSessionService`
  - synchronizes session/Redis state before authenticated cart reads
  - composes mutation delta data such as quantity, line total and cart total
  - provides anonymous cart count responses without leaking HTTP/session concerns into the domain layer
  - clears the authenticated cart

- `ICartSessionService` / `CartSessionService`
  - remains the lower-level session/store synchronization service
  - cart mutations against the session/store layer
  - persistent synchronization and cleanup

- `ICartService` / `CartService`
  - remains the persistent cart service
  - Admin archive queries and archived-cart details

### Cart-specific rule learned

Keep HTTP ownership resolution at the controller boundary: the controller decides whether the cart owner is the authenticated user ID or an anonymous session key. Pass that owner key into the application service. This prevents the domain service from depending directly on `HttpContext` while still allowing authenticated and anonymous carts to share the same orchestration logic.

Admin/reporting endpoints that operate on archived carts belong in a separate Admin controller even when they share the same route base with normal cart endpoints.

---

## Source-of-truth example 5: Merchandise

The original `MerchandiseController` mixed HTTP routing with query normalization, EF Core filtering, price-band construction, review aggregation, rating-facet calculation, pagination and page-model composition.

### Controller

- `MerchandiseController`
  - `GET /api/merchandise`
  - `GET /api/merchandise/List`
  - legacy `/Merchandise` route base remains compatible
  - delegates both endpoints to one service call and only maps the result to HTTP `200 OK`

### Service

- `IMerchandiseService` / `MerchandiseService`
  - normalizes `ProductQueryOptions`
  - search/category/price-range filtering
  - review aggregation
  - sorting and pagination
  - thumbnail projection
  - merchandise page-model composition
  - rating-band facet counts

### Merchandise-specific rules learned

The rating filter is intentionally **banded**, matching V1 behavior; it is not a generic `>= N` threshold. `MinRating=4` means average rating `4.00–4.49`, `MinRating=3` means `3.00–3.49`, and so on. `MinRating=5` remains the V1 perfect-rating band (`5.00`).

`RatingAtLeast` is a legacy property name kept for contract compatibility. Its values are the same V1 rating-band counts rather than cumulative `>=` counts.

Controller refactors must preserve established V1 business semantics unless a behavior change is explicitly requested. Moving logic into a service is not permission to reinterpret filters or business rules.

---

## Source-of-truth example 6: Remaining controller workflow batch

The remaining controllers identified as structural refactor candidates were converted to thin HTTP boundaries without intentionally changing their working behavior.

### Controllers and services

- `PageBuilderController` -> `IPageBuilderAdminService` / `PageBuilderAdminService`
  - page lookup, section/slug normalization, create/edit/delete persistence
- `TalentsController` -> `ITalentsPageService` / `TalentsPageService`
  - spell/item loading, talent-tree composition and key resolution
- `PaladinController` -> `IPaladinPageService` / `PaladinPageService`
  - section service orchestration, combined page models, talent loading and content-page rendering
- `PromoCodesController` -> `IPromoCodeAdminService` / `PromoCodeAdminService`
  - Admin listing, normalization/validation, duplicate checking, creation and deactivation orchestration
- `ItemsController` -> `IItemAdminService` / `ItemAdminService`
  - Admin item CRUD and normalization
- `SpellsController` -> `ISpellAdminService` / `SpellAdminService`
  - Admin spell CRUD and normalization
- `DatabaseController` -> `IAdminDatabaseService` / `AdminDatabaseService`
  - entity selection, search, pagination and Admin database model composition
- `AuthApiController` -> `IAuthService` / `AuthService`
  - registration, login, 2FA login, recovery-code login, password change, session response composition and role workflow
- `AccountSecurityController` -> `IAccountSecurityApplicationService` / `AccountSecurityApplicationService`
  - security overview, authenticator setup/verification, recovery-code generation and logout-all orchestration

### Batch-specific rules learned

- Existing low-level/domain services should remain in place; focused application services coordinate them rather than duplicating them.
- Antiforgery, HTTP status mapping and session reads/writes remain controller concerns.
- Auth/2FA response messages, status codes and authenticator behavior are contracts and must be preserved during structural refactoring.
- Request/response DTOs that are reusable API contracts should live outside controller files.

---

## Pattern for the next large controller

When asked to "refactor this controller like Checkout" or "do the same as Account":

1. Read the entire controller and list each responsibility.
2. Identify existing services before creating new ones.
3. Move business logic into the appropriate existing/new domain services without changing working behavior.
4. Add interfaces where useful for DI/testing.
5. Register new services in application DI startup.
6. Keep controller actions thin.
7. Split the controller only along clear HTTP/domain boundaries.
8. Preserve existing routes, response contracts and established business behavior unless explicitly told to change them.
9. If V1 is the known-working reference for a V2 feature, compare against V1 before treating unusual behavior as a bug.
10. Check for duplicated legacy/API routes and remove them only as a separate migration step.
11. Build/test before committing whenever the environment permits it.
12. If no .NET SDK/CI is available, perform source-level dependency, route and diff checks and state that compile verification is still pending.

## Repository workflow

While PaladinHub is still pre-live and not deployed, controller refactors may be committed directly to `main` when explicitly requested. Once production deployment starts, switch this workflow to feature branches + PR review.
