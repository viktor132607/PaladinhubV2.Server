# Account security and wallet

Set `Resend__ApiKey`, `Resend__FromEmail` (a verified Resend sender),
`Resend__FromName`, and the public frontend `ClientApp__BaseUrl` on the API.
These are the same Resend setting names used by DGVisionStudio. Credentials
remain server-side. Verification and password-reset links expire after 30 minutes.

Production API and frontend must use HTTPS. Identity application, pending 2FA,
remembered-device, session and antiforgery cookies use Secure and SameSite=None
in production. Browsers that block third-party cookies still require client and
API hosting on the same site. Keep data-protection keys persistent and shared
across API instances to preserve tokens/cookies across deployments.

Authenticator secrets are rendered as QR codes locally in the frontend. No
secret is sent to a third-party QR service. Email login codes require a verified
email and opt-in; they are bound to the pending browser session. Identity lockout
applies to failed logins. Account-management requests are rate limited. Recovery
codes are displayed only when generated; regenerating replaces previous codes.

Wallet additions use the configured Stripe test/live secret key and hosted
Checkout. Configure Stripe events `checkout.session.completed` and
`checkout.session.async_payment_succeeded` for:

`POST /api/account/wallet/webhook`

Set its signing secret as `Stripe__WalletWebhookSecret`. Only paid USD wallet
sessions are credited. Checkout session identity determines the transaction's
primary key, so webhook/browser retries and concurrent delivery cannot credit
it twice. Browser confirmation checks account ownership. Allowed top-ups:
USD 1.00–1,000.00, with at most two fractional digits. The legacy `DevTopUp`
endpoint is restricted to the Admin role and is not used by the public UI.

Smoke checks with test accounts and Stripe test mode:

- Verify email; update name/contact phone; confirm an email change.
- Change password; request a reset link; reject invalid/expired tokens.
- Enable email 2FA, sign out, enter password, send and verify email code.
- Add an authenticator, verify its code, save recovery codes, sign in with TOTP.
- Reject an email code from another pending browser session.
- Reject an incorrect password when disabling a factor; preserve the other factor.
- Redeem a wallet code; top up via Checkout; retry confirmation and webhook.
- Confirm that another account cannot finalize a checkout and unpaid checkout
  does not change balance.
