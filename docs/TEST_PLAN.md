# IEP Advisor — Beta Test Plan

**Version:** 1.1
**Date:** 2026-03-15
**Audience:** Beta testers, QA, internal team

> This test plan covers every feature in the IEP Advisor platform. Work through each section in order. Check off items as you complete them. Note any bugs or unexpected behavior in the "Notes" column.

---

## Pre-Requisites

- [ ] An admin has sent you a beta invite email (or you have an invite code)
- [ ] You have access to the deployed app URL
- [ ] You have a sample IEP PDF document to upload (or use any multi-page PDF)
- [ ] You have Google Authenticator or Authy installed (for MFA testing)
- [ ] Check your email (including spam) for invite and password reset emails

---

## 1. Registration & Login

### 1.1 Beta Invite Flow (Admin Side)

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 1.1.1 | As admin, go to Admin > Users | "Invite Beta User" button visible | | |
| 1.1.2 | Click "Invite Beta User", enter email, click "Send Invite" | Success message: "Invite sent to email@example.com" | | |
| 1.1.3 | Check the invited person's email | Branded email with "Create Your Account" button received | | |
| 1.1.4 | Check spam folder if not in inbox | Email from DoNotReply@mail.iep-advisor.com | | |

### 1.2 Registration (Closed Beta)

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 1.2.1 | Click "Create Your Account" link in invite email | Register page opens with invite code auto-filled | | |
| 1.2.2 | Go to `/register` directly (no code in URL) | Registration form shown with empty invite code field | | |
| 1.2.3 | Submit without invite code | Validation error: "Invite code is required" | | |
| 1.2.4 | Submit with invalid invite code | Error: "Invalid or expired invite code" | | |
| 1.2.5 | Submit with valid invite code + all fields | Success: redirected to login with "Registration successful" message | | |
| 1.2.6 | Try to register again with same invite code | Error: "Invalid or expired invite code" (single-use) | | |
| 1.2.7 | Try to register with an already-used email | Error: "Email is already registered" | | |
| 1.2.8 | After registration, check subscription status | Should be "Active" with 1-year expiry (beta grant) | | |

### 1.3 Login

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 1.2.1 | Login with valid credentials | Redirected to dashboard (or onboarding if first time) | | |
| 1.2.2 | Login with wrong password | Error: "Invalid email or password" | | |
| 1.2.3 | Login with non-existent email | Error: "Invalid email or password" (same message — no enumeration) | | |
| 1.2.4 | Login 10+ times with wrong password | Account should be locked for 15 minutes | | |

### 1.4 Password Reset

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 1.4.1 | Click "Forgot password?" on login page | Redirected to forgot password page | | |
| 1.4.2 | Submit email | Success message shown (regardless of whether email exists) | | |
| 1.4.3 | Check email inbox | Branded "Reset Your Password" email received with teal button | | |
| 1.4.4 | Click "Reset Password" button in email | Reset password page opens with token in URL | | |
| 1.4.5 | Enter new password + confirm, submit | Success: redirected to login | | |
| 1.4.6 | Login with new password | Successful login | | |
| 1.4.7 | Try to reuse the reset link | Error: token already used or expired | | |
| 1.4.8 | Wait 15+ minutes, try unused reset link | Error: token expired | | |

---

## 2. Onboarding

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 2.1 | After first login, dashboard shows "Complete your setup" banner | Banner visible with "Get Started" link | | |
| 2.2 | Click "Get Started" → onboarding flow starts | Step 1: Welcome screen shown | | |
| 2.3 | Progress dots show step 1 of 4 | Dots visible, first dot highlighted | | |
| 2.4 | Click "Let's get you set up" | Advance to Step 2: Set State | | |
| 2.5 | Select a state from dropdown, click Continue | State saved, advance to Step 3 | | |
| 2.6 | Skip state step | Advances to Step 3 without saving | | |
| 2.7 | Step 3: Add child with all fields, submit | Child created, advance to Step 4 | | |
| 2.8 | Skip child step | Advances to Step 4 | | |
| 2.9 | Step 4: Feature overview shown | "Go to Dashboard" and "Learn about IEPs" buttons visible | | |
| 2.10 | Click "Go to Dashboard" | Onboarding marked complete, dashboard shown, banner gone | | |
| 2.11 | Revisit `/onboarding` | Should work (not blocked for completed users) | | |

---

## 3. Profile Management

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 3.1 | Navigate to Profile page | Name, email, state, subscription status visible | | |
| 3.2 | Change first name, save | Success message, name updated | | |
| 3.3 | Change state/jurisdiction, save | Success, state updated | | |
| 3.4 | Email field is disabled (not editable) | Cannot change email | | |

---

## 4. Child Management

### 4.1 Creating Children

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 4.1.1 | Navigate to "My Children" | Children list page shown (empty or with existing children) | | |
| 4.1.2 | Click "Add Child" | Create child form shown | | |
| 4.1.3 | Fill in first name only, submit | Child created (other fields optional) | | |
| 4.1.4 | Fill in all fields (name, DOB, grade, disability, district), submit | Child created with all info | | |
| 4.1.5 | Click on a child card | Child detail page shown | | |

### 4.2 Child Detail Page

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 4.2.1 | Profile section shows all child info | DOB, grade, disability, district displayed | | |
| 4.2.2 | Click "Edit" | Edit form with pre-filled values | | |
| 4.2.3 | Change grade level, save | Updated successfully | | |
| 4.2.4 | Click "Remove" | Confirmation prompt, then child removed | | |

---

## 5. Advocacy Goals

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 5.1 | On child detail, "Your Advocacy Goals" section visible | Empty state with "Add Your First Goal" button | | |
| 5.2 | Click "Add Your First Goal" | Goal form appears | | |
| 5.3 | Enter goal text (10+ chars), select category, submit | Goal created with category badge | | |
| 5.4 | Add 2-3 more goals | All displayed in order | | |
| 5.5 | Click edit icon on a goal | Edit form with pre-filled values | | |
| 5.6 | Change goal text, save | Updated | | |
| 5.7 | Click delete icon on a goal | Confirmation, then goal removed | | |
| 5.8 | Click up/down arrows to reorder | Goals reordered | | |
| 5.9 | Try to add more than 10 goals | Message: "Maximum of 10 advocacy goals" | | |
| 5.10 | Goal text less than 10 characters | Validation error | | |

---

## 6. IEP Documents

### 6.1 Creating an IEP Event

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 6.1.1 | On child detail, click "New IEP" | Create IEP form appears | | |
| 6.1.2 | Fill in meeting date and type, submit | IEP created in "created" status | | |
| 6.1.3 | IEP appears in document list with meeting date + type badge | Badge shows meeting type | | |

### 6.2 Uploading a PDF

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 6.2.1 | On an IEP in "created" status, click "Upload PDF" | File upload zone appears | | |
| 6.2.2 | Drag and drop a PDF | Upload starts, status changes to "uploaded" then "processing" | | |
| 6.2.3 | Wait for processing to complete | Status changes to "parsed" | | |
| 6.2.4 | Try uploading a non-PDF file | Error: "Only PDF files are supported" | | |

### 6.3 Viewing a Parsed IEP

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 6.3.1 | Click "View" on a parsed IEP | IEP viewer page with Document + Analysis + Meeting Prep tabs | | |
| 6.3.2 | Document tab shows sections in sidebar | Click sections to navigate | | |
| 6.3.3 | Each section shows extracted text and goals | Readable content | | |
| 6.3.4 | Meeting metadata shown in header (date, type, attendees) | Correct information | | |

---

## 7. IEP Analysis

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 7.1 | On Analysis tab, click "Analyze IEP" | Analysis status changes to "analyzing" | | |
| 7.2 | Wait for analysis to complete (30-60 seconds) | Status changes to "completed", results shown | | |
| 7.3 | Overview section shows plain-language summary | Readable, parent-friendly summary | | |
| 7.4 | Red flags shown with severity badges (yellow/red) | Flags are relevant to the IEP content | | |
| 7.5 | Suggested questions are categorized | Categories: goals, services, placement, rights, general | | |
| 7.6 | Goal Analysis shows SMART criteria ratings | Green/yellow/red for each criterion | | |
| 7.7 | If advocacy goals exist: "Your Goals" tab appears | Gap analysis shows addressed/partially/not addressed | | |
| 7.8 | Change advocacy goals after analysis | Stale analysis banner appears: "Re-analyze?" | | |
| 7.9 | Click "Re-analyze" on banner | New analysis generated with updated goals | | |

---

## 8. Meeting Prep Checklists

### 8.1 From IEP Analysis (Mode A)

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 8.1.1 | On IEP viewer, click "Meeting Prep" tab | Empty state or existing checklist | | |
| 8.1.2 | Click "Generate Meeting Prep" | Processing indicator, then checklist appears | | |
| 8.1.3 | Checklist has 6 sections: Questions, Documents, Red Flags, Rights, Goal Gaps, Tips | All sections populated | | |
| 8.1.4 | Check off items | Strikethrough + muted style, progress bar updates | | |
| 8.1.5 | Refresh page | Checked items persist | | |

### 8.2 From Goals Only (Mode B)

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 8.2.1 | On child detail page, click "Prep for Meeting" | Checklist generation starts (no IEP needed) | | |
| 8.2.2 | Checklist generated based on child info + advocacy goals | Content relevant to stated goals | | |

---

## 9. IEP Version Comparison

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 9.1 | Upload and parse 2+ IEPs for the same child | Both appear in document list | | |
| 9.2 | "IEP Timeline" section on child detail shows chronological list | Both IEPs shown with dates and stats | | |
| 9.3 | Click "Compare" between two IEPs | Comparison page loads | | |
| 9.4 | Goal diff: added goals shown in green, removed in red | Clear visual distinction | | |
| 9.5 | Modified goals show field-by-field changes | Arrow notation: "old value → new value" | | |
| 9.6 | Section diff shows added/removed sections | +/- indicators | | |
| 9.7 | Red flag resolution tracking | Resolved (green), persisting (amber), new (red) | | |

---

## 10. Sharing (Co-Parent / Advocate Access)

### 10.1 Inviting

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 10.1.1 | On child detail, click "Share" (owner only) | Share dialog appears | | |
| 10.1.2 | Enter email + select "Viewer" role, click "Send Invite" | Invite created, email sent to invitee | | |
| 10.1.3 | Check invitee's email | Branded invite email received with "Accept Invitation" button | | |
| 10.1.4 | View access list | Shows the pending invite with email + "Pending" badge | | |

### 10.2 Accepting

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 10.2.1 | As the invited user, click "Accept Invitation" in the email | Accept invite page shown | | |
| 10.2.2 | Invite accepted | Shared child appears in children list with "Shared" badge | | |

### 10.3 Viewer Permissions

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 10.3.1 | As viewer, view child profile | Visible | | |
| 10.3.2 | As viewer, view IEP documents and analysis | Visible | | |
| 10.3.3 | As viewer, try to edit child profile | Edit/Remove buttons hidden | | |
| 10.3.4 | As viewer, try to upload IEP | Upload button hidden | | |
| 10.3.5 | As viewer, try to add advocacy goals | Add Goal button hidden | | |

### 10.4 Revoking

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 10.4.1 | As owner, click "Revoke" on a shared user | Access removed | | |
| 10.4.2 | As revoked user, refresh page | Child no longer visible | | |

---

## 11. Subscription & Usage

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 11.1 | Dashboard shows subscription status card | "Active" badge with expiry date | | |
| 11.2 | Profile page shows subscription section | Status + per-child usage bars | | |
| 11.3 | Navigate to `/subscription` | Full subscription page with usage details | | |
| 11.4 | Run 5 analyses on the same child | Each decrements the usage counter | | |
| 11.5 | Try to run a 6th analysis | Error: "Analysis limit reached for this child" (429) | | |
| 11.6 | "Manage Subscription" button (for Stripe subscribers) | Redirects to Stripe Customer Portal | | |

---

## 12. Knowledge Base

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 12.1 | Navigate to Knowledge Base from sidebar | Page loads with all entries | | |
| 12.2 | Type in search box | Results filter with 300ms debounce | | |
| 12.3 | Search for "FAPE" | Matching entries shown | | |
| 12.4 | Click category tabs (Rights, Provisions, Glossary, Process, Tips) | Entries filtered by category | | |
| 12.5 | Tab counts match visible entries | Numbers are accurate | | |
| 12.6 | Each entry shows title, content, legal reference, tags | Properly formatted | | |
| 12.7 | Navigate to IEP 101 page | Static educational content shown | | |
| 12.8 | "Explore our full Knowledge Base" link works | Links to `/knowledge-base` | | |

---

## 13. MFA (Multi-Factor Authentication)

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 13.1 | On profile page, find MFA section | "Enable MFA" button visible | | |
| 13.2 | Click "Enable MFA" → navigate to MFA setup | QR code displayed + manual entry key | | |
| 13.3 | Scan QR code with authenticator app | Code starts generating in the app | | |
| 13.4 | Enter 6-digit code, click Verify | MFA enabled, 10 recovery codes shown | | |
| 13.5 | **Save the recovery codes** | Codes displayed only once | | |
| 13.6 | Log out, log back in | After password: MFA code prompt shown | | |
| 13.7 | Enter valid 6-digit code | Login successful | | |
| 13.8 | Enter wrong code 5+ times | Account locked for 15 minutes | | |
| 13.9 | Use "recovery code" option | Recovery code accepted, login successful | | |
| 13.10 | Same recovery code cannot be reused | Error on second use | | |
| 13.11 | Disable MFA (requires password + current code) | MFA disabled, normal login restored | | |

---

## 14. Admin Features

### 14.1 Admin Dashboard

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 14.1.1 | As admin, sidebar shows "Admin" section | Dashboard + Users links visible | | |
| 14.1.2 | Navigate to Admin Dashboard | Stats cards: users, children, documents, analyses | | |
| 14.1.3 | Stats show correct counts | Cross-reference with database | | |
| 14.1.4 | Recent users table shows latest signups | Names, emails, dates visible | | |
| 14.1.5 | As non-admin, try to access `/admin` | Redirected to dashboard | | |

### 14.2 User Management

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 14.2.1 | Navigate to Admin > Users | User list with role/status badges | | |
| 14.2.2 | Search users by name or email | List filters | | |
| 14.2.3 | Click on a user | User detail page shown | | |
| 14.2.4 | Change user role to Admin, save | Role updated | | |
| 14.2.5 | Deactivate a user | User shows as Inactive | | |
| 14.2.6 | Deactivated user tries to login | Login fails (or existing session invalidated) | | |
| 14.2.7 | Reactivate the user | User can login again | | |

### 14.3 Beta User Invites

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 14.3.1 | On admin users page, click "Invite Beta User" | Invite form appears with email input | | |
| 14.3.2 | Enter a valid email, click "Send Invite" | Success message shown | | |
| 14.3.3 | Check the invited email's inbox | Branded invite email with "Create Your Account" link | | |
| 14.3.4 | Click the link in the email | Register page opens with invite code pre-filled | | |
| 14.3.5 | Invite the same email again | Should still work (generates a new code) | | |

### 14.4 Email Delivery

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 14.4.1 | Beta invite email | Delivered from DoNotReply@mail.iep-advisor.com, branded HTML | | |
| 14.4.2 | Password reset email | Delivered, "Reset Password" teal button works | | |
| 14.4.3 | Share invite email | Delivered, shows inviter name + child name + role | | |
| 14.4.4 | Check spam scoring | Emails should not land in spam (check multiple providers) | | |

---

## 15. Account Management

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 15.1 | Profile page shows "Export My Data" button | Button visible | | |
| 15.2 | Click "Export My Data" | JSON file downloads with all user data | | |
| 15.3 | Profile page shows "Delete Account" button | Button visible (red/danger styling) | | |
| 15.4 | Click "Delete Account" | Requires password confirmation | | |
| 15.5 | Confirm deletion | Account scheduled for deletion, logged out | | |

---

## 16. Security & Edge Cases

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 16.1 | Access any protected route while logged out | Redirected to login | | |
| 16.2 | API returns 401 | Automatically redirected to login | | |
| 16.3 | Try to access another user's child by URL | 404 or empty response (no data leak) | | |
| 16.4 | Upload a non-PDF file renamed to .pdf | Error: "File does not appear to be a valid PDF" | | |
| 16.5 | Rapid-fire API requests | Rate limiting returns 429 | | |
| 16.6 | Long page load / slow API | Loading spinners shown (not blank screens) | | |
| 16.7 | Browser back/forward navigation | Pages render correctly, no stale data | | |
| 16.8 | Mobile responsive | Sidebar collapses to hamburger, content readable | | |

---

## 17. Brand & UX Consistency

| # | Test Case | Expected Result | Pass? | Notes |
|---|-----------|-----------------|-------|-------|
| 17.1 | All headings use Lora serif font | Consistent across all pages | | |
| 17.2 | All body text uses DM Sans | Consistent | | |
| 17.3 | Primary buttons are teal (#1A9478) | No blue-600 buttons anywhere | | |
| 17.4 | Cards have 12px radius, 0.5px slate border | Consistent styling | | |
| 17.5 | Warning elements use amber (#D4820F) | Consistent | | |
| 17.6 | Error elements use red (#B91C1C) | Consistent | | |
| 17.7 | Logo lockup displays correctly | Teal checkmark + "IEP Advisor" | | |
| 17.8 | All icons are Lucide with 1.8px stroke | No mismatched icon styles | | |

---

## Bug Report Template

When you find an issue, please report it with:

```
**Page/Feature:** [e.g., Child Detail > Advocacy Goals]
**Steps to Reproduce:**
1. ...
2. ...
3. ...
**Expected:** [what should happen]
**Actual:** [what actually happened]
**Browser:** [Chrome/Safari/Firefox + version]
**Screenshots:** [attach if possible]
```

---

## Test Environment Notes

- **URL:** [deployment URL]
- **Admin account:** [email] (ask the team lead)
- **Beta invites:** Admin sends invites from Admin > Users > "Invite Beta User"
- **Emails sent from:** DoNotReply@mail.iep-advisor.com
- **Sample IEP PDF:** [link to test document if available]
- **Sentry:** Check [sentry.io] for frontend error reports after testing
- **Elastic APM:** Check Kibana for backend performance/errors
