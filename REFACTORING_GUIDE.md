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

## Pattern for the next large controller

When asked to "refactor this controller like Checkout" or "do the same as Account":

1. Read the entire controller and list each responsibility.
2. Identify existing services before creating new ones.
3. Move business logic into the appropriate existing/new domain services.
4. Add interfaces where useful for DI/testing.
5. Register new services in application DI startup.
6. Keep controller actions thin.
7. Split the controller only along clear HTTP/domain boundaries.
8. Preserve existing routes and response contracts unless explicitly told to change them.
9. Check for duplicated legacy/API routes and remove them only as a separate migration step.
10. Build/test before committing whenever the environment permits it.
11. If no .NET SDK/CI is available, perform source-level dependency, route and diff checks and state that compile verification is still pending.

## Repository workflow

While PaladinHub is still pre-live and not deployed, controller refactors may be committed directly to `main` when explicitly requested. Once production deployment starts, switch this workflow to feature branches + PR review.
