# PaladinHub Controller Refactoring Guide

This file is the reference pattern for future controller refactors in `PaladinHubV2.Server`.

## Source-of-truth example: Checkout

The original `CheckoutController` mixed HTTP concerns with checkout state management, cart synchronization, wallet charging, Stripe calls, transaction persistence, order finalization and response routing.

The refactor follows this rule:

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

## Checkout implementation

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

## Pattern for the next large controller

When asked to "refactor this controller like Checkout":

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

## Repository workflow

While PaladinHub is still pre-live and not deployed, controller refactors may be committed directly to `main` when explicitly requested. Once production deployment starts, switch this workflow to feature branches + PR review.
