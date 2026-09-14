# Meta App Review & Business Verification Guide

**Verification Date:** September 14, 2026  
**Applicability:** Commercial Physical Instagram Follower Counter Phase 1

---

## 1. Development Mode vs. Live/Production Mode

### In Development Mode:
- The Meta app can be created instantly in [Meta for Developers](https://developers.facebook.com/).
- Only administrators, developers, and test users added explicitly under the **App Roles** tab can perform the OAuth authorization flow.
- Tokens issued in Development Mode work with the Instagram API with Instagram Login, allowing end-to-end testing of the authorization flow, long-lived token exchange, token refresh, and follower count retrieval without requiring App Review.
- Rate limits are lower, but sufficient for internal hardware testing and staging.

### In Live / Production Mode:
- Any Instagram creator or business customer worldwide can authorize your app.
- Transitioning to Live Mode **requires Meta App Review** and **Meta Business Verification**.

---

## 2. Meta Business Verification (Prerequisite)

Before submitting for App Review, Meta requires legal verification of the business entity operating the product:
1. **Legal Business Details:** Official business name, physical address, phone number, and website domain matching the brand.
2. **Supporting Documentation:** Certificate of Incorporation, Business License, or Tax Registration Document.
3. **Domain Verification:** Verification of the website domain (e.g., `https://counter.example.com`) via DNS TXT record or HTML meta tag.
4. **Business Manager Association:** The Meta App must be owned by the verified Meta Business Account.

---

## 3. Required App Review Submissions

For our commercial follower counter product, only **one single permission** is required:

### Permission: `instagram_business_basic`
- **Use Case:** "Allow physical counter owners to authenticate their Instagram Creator or Business account, retrieve their public profile identifier and username, and periodically synchronize their live follower count to physically move the mechanical split-flap/LED display digits."
- **Data Accessed:** Account ID, Username, Account Type, and `followers_count`.
- **Review Requirements:**
  1. **Screencast Walkthrough Video:** A recording demonstrating the complete end-to-end user experience:
     - User registers/logs into our customer web portal.
     - User clicks "Connect Instagram".
     - The official Meta authorization screen opens.
     - User approves permissions.
     - Backend captures follower count.
     - Physical device reflects the live follower count.
  2. **Step-by-step Test Instructions:** Detailed login credentials for a test customer account on our web portal, and instructions explaining how our background worker reads the follower count.
  3. **Privacy Policy & Terms of Service:** Publicly accessible HTTPS URLs detailing how customer follower data is securely processed, encrypted, and that user credentials/passwords are never collected or stored.
  4. **Data Deletion Instructions:** Clear instructions (and our working `/api/v1/auth/delete-account` endpoint) showing how users can disconnect their Instagram account and delete all stored tokens.

---

## 4. Submission Checklist

- [x] Meta Developer App created (App ID: `1337614034893316`).
- [x] Product "Instagram" added with "Instagram API with Instagram Login".
- [x] Valid OAuth Redirect URIs configured in App Dashboard:
      - Development: `https://localhost:7149/api/v1/instagram/callback`
      - Staging: `https://staging-api.example.com/api/v1/instagram/callback`
      - Production: `https://api.example.com/api/v1/instagram/callback`
- [x] Development Mode verified live with real Instagram Creator account (`@arun_naturals_official`).
- [ ] Meta Business Account fully verified.
- [ ] Privacy Policy URL and Terms of Service URL active.
- [ ] High-resolution app icon uploaded.
- [ ] Screencast video recorded and attached to `instagram_business_basic` review item.
- [ ] App Review submitted and approved.
- [ ] App switched to "Live" mode.
