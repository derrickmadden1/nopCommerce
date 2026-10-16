# nopCommerce Local End-to-End Sandbox Checkout Tests

Automated Playwright + NUnit checkout test suite for local end-to-end testing against the development branch and local database (`rcc_azure_20260801_2` on `SQLEXPRESS`).

Unlike the Azure Checkout Monitor (which halts before payment to avoid live charges), this suite tests **complete checkout flows all the way to order completion** (`/checkout/completed`) using sandbox gateway accounts.

---

## What is Covered

1. **Square Sandbox Checkout Flow** (`SquareCheckoutTests.cs`)
   - Adds test SKU (`/system-verification-item`) to basket.
   - Proceeds through One-Page Checkout (OPC) or multi-step checkout.
   - Selects **Credit / Debit Card (Square)**.
   - Populates Square Web Payments SDK iframes (Card Number, Expiration, CVV, Postal Code).
   - Handles the CardinalCommerce 3DS modal challenge if enabled.
   - Clicks payment submission, asserts order completion, and captures the order number.

2. **PayPal Commerce Sandbox Checkout Flow** (`PayPalCheckoutTests.cs`)
   - Tests PayPal smart button popup checkout.
   - Automatically signs into the configured PayPal Sandbox Buyer account.
   - Approves transaction, redirects back to store, and confirms order number.
   - *Note: Test automatically skips with an informative message if buyer credentials are not configured.*

3. **Manual / Offline Payment Flow** (`ManualCheckoutTests.cs`)
   - Tests standard store checkout pipeline with zero external gateway dependencies for quick sanity checking.

---

## Configuration & Sandbox Secrets

Configuration is managed via `testsettings.json` (gitignored). A template is provided in `testsettings.template.json`.

```json
{
  "CheckoutTest": {
    "BaseUrl": "https://localhost:58552",
    "Headless": false,
    "SlowMo": 300,
    "TestProductSlug": "system-verification-item",
    "Customer": {
      "FirstName": "Test",
      "LastName": "Buyer",
      "Email": "e2e-test@rosecottagecroft.co.uk",
      "Country": "United Kingdom"
    },
    "Square": {
      "TestCardNumber": "4111111111111111",
      "Expiration": "12/28",
      "Cvv": "123",
      "PostalCode": "94103",
      "Handle3ds": true,
      "3dsSmsCode": "1234"
    },
    // Note: Default Square Sandbox card (4111...) requires a 5-digit US ZIP (e.g. 94103) for SDK tokenization.
    "PayPal": {
      "SandboxBuyerEmail": "your-sandbox-buyer@personal.example.com",
      "SandboxBuyerPassword": "your-sandbox-password"
    }
  }
}
```

You can also override settings via environment variables:
- `CHECKOUT_BASE_URL` (e.g. `https://localhost:58552` or local IIS URL)
- `CHECKOUT_HEADLESS` (`true` or `false`)
- `PAYPAL_SANDBOX_BUYER_EMAIL`
- `PAYPAL_SANDBOX_BUYER_PASSWORD`

---

## Dev Database Safeguards

To prevent accidentally triggering live payment APIs or blasting real customer email inboxes:
- Run `Scripts/Apply-DevSandboxSettings.sql` (or let `Run-DevCheckoutTests.ps1` execute it automatically).
- **Enforces Sandbox Mode**:
  ```sql
  UPDATE Setting SET Value = 'True' WHERE Name IN ('squarepaymentsettings.usesandbox', 'paypalcommercesettings.usesandbox');
  ```
- **Disables Email Worker**:
  ```sql
  UPDATE ScheduleTask SET Enabled = 0 WHERE Id = 1;
  ```
  *(Emails are safely recorded in `QueuedEmail` table for inspection, but never dispatched to SendGrid).*
- **Ensures Verification SKU Exists**:
  Seeds `system-verification-item` (£0.01, published, stock 9999).

---

## Running the Tests

### Option A: Single-Command PowerShell Orchestrator
Run the test runner from pwsh:
```powershell
pwsh src/Tests/Nop.Tests.E2E/Scripts/Run-DevCheckoutTests.ps1
```

**Options:**
- Run only Square tests:
  ```powershell
  pwsh src/Tests/Nop.Tests.E2E/Scripts/Run-DevCheckoutTests.ps1 -Filter "Category=Square"
  ```
- Run headless:
  ```powershell
  pwsh src/Tests/Nop.Tests.E2E/Scripts/Run-DevCheckoutTests.ps1 -Headless
  ```
- If your store is already running via Visual Studio (F5):
  The script automatically detects the running server and executes tests immediately against it without launching a second instance.

### Option B: Visual Studio Test Explorer
1. Launch `Nop.Web` in Visual Studio (F5 or Ctrl+F5) so `https://localhost:58552` is running.
2. Open **Test Explorer** (`Ctrl+E, T`).
3. Expand `Nop.Tests.E2E` -> Right-click any test (e.g. `Square_Sandbox_Checkout_Places_Order_Successfully`) and select **Run** or **Debug**.
4. In headed mode (`Headless: false`), you can watch Chromium fill the forms, handle Square's iframes, and complete the order.

### Option C: `dotnet test` CLI
```bash
dotnet test src/Tests/Nop.Tests.E2E/Nop.Tests.E2E.csproj --logger "console;verbosity=normal"
```

---

## Failure Diagnostics & Screenshots

If a checkout flow fails:
1. Playwright automatically captures a full-page screenshot of the failing step.
2. Screenshots are saved to `src/Tests/Nop.Tests.E2E/bin/Debug/net10.0/Screenshots/`.
3. The screenshot file path is printed in the test output and attached directly to the test result in Visual Studio Test Explorer.
