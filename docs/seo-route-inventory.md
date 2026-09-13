# SEO route inventory

Source reviewed for point 14.1: `PaladinhubV2.Client/src/App.tsx` on the current client `main` as of 2026-09-13. The server-side `SeoRouteRegistry` is the allow-list used by the SEO API. API/controller routes are never selectable SEO page targets.

## Public indexable static routes

Canonical routes:
- `/`
- `/Holy/Overview`, `/Holy/Gear`, `/Holy/Talents`, `/Holy/Consumables`, `/Holy/Rotation`, `/Holy/Stats`
- `/Protection/Overview`, `/Protection/Gear`, `/Protection/Talents`, `/Protection/Consumables`, `/Protection/Rotation`, `/Protection/Stats`
- `/Retribution/Overview`, `/Retribution/Gear`, `/Retribution/Talents`, `/Retribution/Consumables`, `/Retribution/Rotation`, `/Retribution/Stats`
- `/discussions`
- `/products`
- `/privacy`

Existing aliases are public but are not independent SEO targets:
- `/Home/Home` -> `/`
- `/Discussion/Index` and `/Discussions/Index` -> `/discussions`
- `/Merchandise/Merchandise` and `/Merchandise/List` -> `/products`
- `/Home/Privacy` -> `/privacy`

## Public dynamic routes

These are real public route patterns but are not static SEO targets in point 14. They need resource-specific metadata if/when static generation is added for those resources:
- `/Discussions/Details/:id`
- `/Products/Details/:id`
- `/products/:id`

Database content uses `/:section/:slug`. SEO for these pages is stored by stable `ContentPage.Id`; the current slug is resolved at read/build time so a slug rename does not detach the SEO record.

## Public but intentionally non-indexable UI routes

- `/Discussions/Create`
- `/Error/404`
- `/Error/500`
- `/error`

## Authentication/account routes

- `/Account/Login`, `/login`
- `/Account/Register`, `/register`
- `/Account/LoginWith2fa`
- `/Account/RecoveryCodeLogin`
- `/Account/VerifyEmail`
- `/Account/ShowRecoveryCodes`
- `/Account/MyAccount`, `/account`
- `/Account/AccountDetails`
- `/Account/ChangePassword`
- `/Account/Connections`
- `/Account/Enable2FA`
- `/Account/PaymentMethods`
- `/Account/AddPaymentMethod`
- `/Account/Privacy`
- `/Account/Security`
- `/Account/Settings`
- `/Account/TransactionHistory`

These are not SEO targets.

## Cart/checkout/private flow routes

- `/Cart/MyCart`, `/cart`
- `/Cart/Details/:id`
- `/Cart/Archive`
- `/Products/Add/:id`
- `/Checkout/Start`, `/checkout`
- `/Checkout/Shipping`
- `/Checkout/Payment`
- `/Checkout/Card`
- `/Checkout/Review`
- `/Checkout/Registered`
- `/Checkout/Success`
- `/Checkout/Failure`
- `/Home/ThanksForPurchasing`

These are not SEO targets.

## Admin routes

All `/Admin` routes are private and excluded. Current concrete client routes are `/Admin`, `/Admin/Database`, `/Admin/Categories`, `/Admin/Classes`, `/Admin/PageBuilder/History`, `/Admin/Footer`, `/Admin/Banners`, `/Admin/Translations`, `/Admin/Navigation`, `/Admin/Media`, `/Admin/Rarities`, `/Admin/Patches`, `/Admin/Tags`, `/Admin/Database/Index`, `/Admin/Items/Create`, `/Admin/Items/Edit/:id`, `/Admin/Items/Details/:id`, `/Admin/Items/Delete/:id`, `/Admin/Spells/Create`, `/Admin/Spells/Edit/:id`, `/Admin/Spells/Details/:id`, `/Admin/Spells/Delete/:id`, `/Admin/PageBuilder`, `/Admin/PageBuilder/Index`, `/Admin/PageBuilder/TalentTrees`, `/Admin/PageBuilder/Create`, `/Admin/PageBuilder/Edit`, `/Admin/PageBuilder/DeleteConfirm`, `/Admin/PageBuilder/Delete`, `/Admin/Products/Create`, `/Admin/Products/Edit/:id`, `/Admin/PromoCodes`, `/Admin/PromoCodes/Index`, and `/Admin/PromoCodes/Create`.

The separately declared `/Products/Create` and `/Products/Edit/:id` routes are also admin-only and are excluded.

## Preview routes

No explicit client preview route was present in the reviewed `App.tsx`. A future preview route must be registered as non-selectable before it can be exposed by the SEO target API.

## API routes

All ASP.NET controller/API routes (`/api/*`, `/Admin/api/*`, account/store mutation endpoints and other server endpoints) are application/API endpoints, not page targets. The SEO API does not accept arbitrary paths; therefore adding a new server endpoint cannot make it SEO-selectable accidentally.
