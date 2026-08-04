---
title: "feat: Closed Beta Registration — Require Invite Code to Sign Up"
type: feat
status: active
date: 2026-03-15
---

# feat: Closed Beta Registration — Require Invite Code to Sign Up

## Overview

Lock down registration to require a valid beta invite code. No one can sign up without one. When a valid code is provided during registration, it's automatically redeemed — granting the user a free 1-year subscription. Admins generate and manage codes from the admin panel.

This replaces open registration with a closed beta model.

## What Changes

### Backend

1. **Modify `POST /api/auth/register`** — add required `inviteCode` field to `RegisterRequest`. During registration:
   - Validate the invite code exists, is active, not expired, not redeemed
   - Create the user
   - Redeem the code (set `RedeemedByUserId`, `RedeemedAt`)
   - Set `SubscriptionStatus = "active"`, `SubscriptionExpiresAt = 1 year`
   - All in one transaction

2. **Add "Invite User" to admin panel** — on the admin users page, add a button that generates a new beta code and displays it (for the admin to share via email/message). Reuses the existing `POST /api/admin/beta-codes` endpoint.

### Frontend

1. **Modify RegisterPage** — add "Invite Code" input field (required). Show validation error if code is invalid.

2. **Modify RegisterRequest type** — add `inviteCode: string`

3. **Add "Generate Invite" UI to admin users page** — button that generates a code and shows it in a copyable format.

## Acceptance Criteria

- [ ] Registration requires a valid beta invite code
- [ ] Invalid/expired/redeemed codes show clear error message
- [ ] Valid code auto-redeems on registration (grants 1-year free subscription)
- [ ] Admin can generate invite codes from admin users page
- [ ] Generated code displayed in copyable format
- [ ] Existing users (already registered) are not affected

## Files to Modify

### Backend
| File | Change |
|------|--------|
| `api/IepAssistant.Api/DTOs/Auth/RegisterRequest.cs` | Add `[Required] InviteCode` field |
| `api/IepAssistant.Services/Implementations/AuthService.cs` | Validate + redeem invite code during registration |
| `api/IepAssistant.Services/Models/AuthModels.cs` | Add `InviteCode` to `RegisterModel` |

### Frontend
| File | Change |
|------|--------|
| `web/src/types/api.ts` | Add `inviteCode` to `RegisterRequest` |
| `web/src/features/auth/components/register-page.tsx` | Add invite code input field |
| `web/src/features/admin/components/admin-users-page.tsx` | Add "Generate Invite" button + code display |

## Sources
- Beta invite code entity: `api/IepAssistant.Domain/Entities/BetaInviteCode.cs`
- Subscription service: `api/IepAssistant.Services/Implementations/SubscriptionService.cs` (RedeemBetaCodeAsync)
- Admin beta code endpoints: `api/IepAssistant.Api/Controllers/StripeController.cs`
