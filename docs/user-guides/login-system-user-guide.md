# NoLock Social Login System - User Guide

## Table of Contents
1. [Overview](#overview)
2. [Getting Started](#getting-started)
3. [Login Process](#login-process)
4. [Remember Me Feature](#remember-me-feature)
5. [Security Best Practices](#security-best-practices)
6. [Troubleshooting](#troubleshooting)
7. [Frequently Asked Questions](#frequently-asked-questions)

---

## Overview

NoLock Social uses a unique login system based on **deterministic identity generation**. Instead of traditional account registration, your username and passphrase mathematically generate your identity keys. This means:

- **No registration required** - Your credentials create your account
- **No password resets** - Your passphrase IS your identity
- **No servers storing passwords** - Everything is generated locally
- **Complete privacy** - Your data belongs only to you

### Key Concepts

**Username**: A public identifier that helps generate your unique identity. This can be remembered by the browser for convenience.

**Passphrase**: A private phrase that, combined with your username, creates your unique cryptographic keys. This must be kept secret and is never stored.

**Identity Keys**: Mathematical keys derived from your username + passphrase that allow you to sign content and prove ownership.

---

## Getting Started

### First Time Login (New User)

1. **Open NoLock Social** in your web browser
2. **Enter a Username** - Choose something memorable (this can be stored for convenience)
3. **Create a Strong Passphrase** - This is your key to your identity, make it strong!
4. **Optionally check "Remember my username"** - Saves only your username, never your passphrase
5. **Click "Create Identity"**

You'll see a welcome message: **"Welcome to NoLock, [username]!"** 

Your identity has been created and you're now logged in. Your first piece of content will mark you as a "returning user" for future logins.

### Returning User Login

1. **Open NoLock Social**
2. **Enter your Username** (may be pre-filled if you chose to remember it)
3. **Enter your Passphrase** (same one you used before)
4. **Click "Login"**

You'll see: **"Welcome back, [username]!"**

The system recognizes you as a returning user because you have existing content associated with your identity.

---

## Login Process

### What Happens During Login

1. **Key Derivation** (~1-2 seconds)
   - Your passphrase + username are processed through PBKDF2 with 100,000+ iterations
   - This generates your unique Ed25519 key pair
   - The process is intentionally slow for security

2. **Identity Verification**
   - The system checks if your public key has any associated content
   - New users see a "Welcome" message
   - Returning users see a "Welcome back" message

3. **Session Start**
   - Your keys are stored securely in memory only
   - A session token is created for this browser tab
   - Your login state becomes active

### Visual Indicators

- **Loading State**: "Deriving keys..." with progress bar during login
- **Success State**: Welcome message and login form disappears
- **Error State**: Clear error message if login fails
- **Logged In State**: Shows your username and logout button

---

## Remember Me Feature

### What Gets Remembered

✅ **Username** - Stored in browser's localStorage for convenience  
✅ **Last used timestamp** - When you last logged in  
❌ **Passphrase** - NEVER stored anywhere  
❌ **Identity Keys** - NEVER persisted  
❌ **Session Data** - Cleared on logout  

### Managing Remembered Data

**To use Remember Me:**
1. Check the "Remember my username" box during login
2. Your username will be pre-filled on future visits

**To forget remembered username:**
1. Click "Forget saved username" link below the login form
2. Username field will be cleared and won't be remembered

**To clear manually:**
- Open browser Developer Tools (F12)
- Go to Application → Local Storage
- Remove the "nolock_remembered_user" entry

### Privacy & Security

The Remember Me feature is designed with privacy in mind:
- Only your username is stored locally
- No sensitive information leaves your browser
- You can clear remembered data anytime
- Remembering username has no security implications

---

## Security Best Practices

### Creating a Strong Passphrase

**Recommended:**
- At least 12 characters long (system minimum)
- Mix of words, numbers, and symbols
- Something memorable to you but hard to guess
- Consider using a passphrase generator

**Example good passphrases:**
- `coffee-mountain-eagle-47`
- `BlueSky@Dawn#2024!`
- `my-dog-loves-tennis-balls-99`

**Avoid:**
- Dictionary words alone
- Personal information (birthdate, name, address)
- Common patterns (123456, password, qwerty)
- Reusing passphrases from other services

### Protecting Your Identity

**Critical Security Rules:**

1. **Never share your passphrase** - It IS your identity
2. **Remember your passphrase** - No recovery possible if forgotten
3. **Use unique passphrases** - Don't reuse from other sites
4. **Consider password managers** - For generating and storing passphrases
5. **Logout when finished** - Clears keys from memory

### Browser Security

**Safe practices:**
- Use updated browsers with security patches
- Avoid public/shared computers for sensitive activities
- Clear browser data if using a public computer
- Enable browser security features (HTTPS-only, etc.)

### What Happens to Your Data

**During Active Session:**
- Keys stored in secure browser memory only
- Session token valid for current browser tab
- All cryptographic operations happen locally

**On Logout:**
- All keys wiped from memory immediately
- Session tokens invalidated
- Only username remains (if remembered)

**Storage Security:**
- No sensitive data ever stored on disk
- Content signed with your keys proves authenticity
- Your identity exists only while you're logged in

---

## Troubleshooting

### Common Issues

#### "Login Failed" Error
**Possible causes:**
- Incorrect passphrase (most common)
- Browser blocking localStorage access
- Network connectivity issues during key derivation

**Solutions:**
1. Double-check your passphrase - it's case sensitive
2. Try refreshing the page and logging in again
3. Clear browser cache and cookies for the site
4. Check if you have JavaScript enabled

#### Login Takes Too Long
**Expected behavior:**
- Key derivation should take 1-2 seconds
- This is intentional for security (PBKDF2 iterations)

**If it takes much longer:**
1. Check browser performance (close other tabs)
2. Restart browser if it seems frozen
3. Try a different browser as a test

#### Username Not Remembered
**Check:**
1. Did you check "Remember my username" during login?
2. Is localStorage enabled in your browser?
3. Did you clear browser data recently?
4. Are you using private/incognito mode?

#### Can't See My Old Content
**Most likely cause:** Wrong passphrase
- Each passphrase generates completely different keys
- Only the exact same username + passphrase combination recreates your identity
- There is no recovery method if you forget your passphrase

#### Multiple Browser Tabs
**Expected behavior:**
- Login in one tab affects all tabs on the same site
- Logout in one tab logs out all tabs
- Each tab shares the same session state

### Browser Compatibility

**Fully Supported:**
- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

**Requirements:**
- JavaScript enabled
- localStorage access
- WebCrypto API support

### Performance Issues

**Normal Performance:**
- Initial component render: <100ms
- Login process: 1-2 seconds
- UI interactions: <50ms response time

**If performance is poor:**
1. Close other browser tabs
2. Restart browser
3. Check available system memory
4. Update to latest browser version

---

## Frequently Asked Questions

### Account Management

**Q: How do I create an account?**
A: There's no separate registration. Just enter a username and passphrase - this creates your account automatically.

**Q: What if I forget my passphrase?**
A: Unfortunately, there's no recovery method. Your passphrase mathematically generates your identity. Consider using a password manager to safely store it.

**Q: Can I change my username?**
A: No. Your username + passphrase combination creates your unique identity. Changing either would create a completely different identity.

**Q: Can I change my passphrase?**
A: Not directly. You would need to create a new identity with a new passphrase, then manually migrate your content.

### Privacy & Security

**Q: Who can see my username?**
A: Your username may be visible in any content you create and sign. Consider this when choosing a username.

**Q: Is my data encrypted?**
A: Yes. All content is signed with your cryptographic keys, proving authenticity and ownership.

**Q: Can NoLock Social access my passphrase?**
A: No. Your passphrase never leaves your browser and is never sent to any server. It's processed locally to generate your keys.

**Q: What happens if the NoLock Social service goes down?**
A: Your identity and content are cryptographically yours. The keys are deterministically generated from your credentials, so your data remains accessible through the cryptographic methods regardless of the service.

### Technical Questions

**Q: Why does login take 1-2 seconds?**
A: We use PBKDF2 with 100,000+ iterations to securely derive your keys. This intentional delay makes it extremely difficult for attackers to crack passphrases.

**Q: Can I use NoLock Social offline?**
A: The core cryptographic functions work offline, but you need internet connectivity to sync content with other users and the distributed storage network.

**Q: Is there a mobile app?**
A: Currently NoLock Social runs in web browsers. The web interface is mobile-responsive and works on mobile browsers.

**Q: Can I export my data?**
A: Your data is inherently portable since it's cryptographically signed. The content-addressable storage system means your data exists independently of any single service.

### Usage Questions

**Q: How do I know if I'm a new or returning user?**
A: The system detects this automatically based on whether you have any existing content associated with your keys. New users see "Welcome to NoLock!" while returning users see "Welcome back!"

**Q: Can I use the same credentials on different devices?**
A: Yes! Your credentials work on any device/browser. Just enter the same username and passphrase to access your identity.

**Q: What if I accidentally logout?**
A: Just log back in with the same credentials. Your identity and content will be restored immediately.

**Q: How long do sessions last?**
A: Sessions remain active as long as your browser tab is open. The system may implement timeout periods for security, but you can extend sessions when needed.

---

## Getting Help

### Support Resources

1. **Check this User Guide** - Most questions are covered here
2. **Review Error Messages** - They provide specific guidance
3. **Try Troubleshooting Steps** - Often resolves common issues
4. **Community Forums** - Other users may have faced similar issues
5. **Developer Documentation** - For technical details

### Reporting Issues

If you encounter bugs or problems:

1. **Note the exact error message** if any
2. **Record the steps** that led to the issue
3. **Check browser console** (F12 → Console) for technical errors
4. **Try reproducing** the issue to confirm it's consistent
5. **Report with details** to the development team

### Best Practices for Getting Help

**Provide details:**
- Browser type and version
- Operating system
- Exact error messages
- Steps to reproduce the issue
- What you expected to happen vs. what actually happened

**Before asking:**
- Try the troubleshooting steps in this guide
- Check if others have reported similar issues
- Verify your credentials are correct

---

## Conclusion

NoLock Social's login system provides a secure, private, and decentralized way to establish your digital identity. By understanding how it works and following security best practices, you can safely enjoy the benefits of truly owning your data and identity online.

Remember the key principle: **Your username + passphrase = Your identity.** Keep your passphrase secure, and you maintain complete control over your NoLock Social presence.

---

*Last Updated: August 14, 2025*  
*Version: 1.0*  
*For technical documentation, see: `/docs/architecture/security/minimal-login-architecture.md`*