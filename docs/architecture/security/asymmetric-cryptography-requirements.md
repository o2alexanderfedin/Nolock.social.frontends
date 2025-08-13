# Asymmetric Cryptography MVP Requirements

## Document Information
- **Version**: 2.0.0
- **Date**: 2025-08-13
- **Status**: MVP Requirements - Deterministic Key Derivation
- **Purpose**: Content authorship authentication for decentralized social network

## 1. Overview

This document defines MVP requirements for asymmetric cryptography to authenticate content authorship in NoLock.social. Users must be able to prove they created content by signing its hash with their private key. Keys are deterministically derived from a passphrase and username combination - no key storage is required.

**Key Approach**: Single Ed25519 key pair deterministically derived from passphrase + username.  
**Scope**: Digital signatures for content authentication only.  
**Out of scope**: Encryption, key exchange, certificates, key recovery, multi-signature, key storage.

## 2. Functional Requirements

### 2.1 Key Pair Derivation
- System SHALL derive Ed25519 key pairs deterministically from passphrase and username
- System SHALL use Argon2id KDF to derive 32-byte seed from passphrase + username
- System SHALL generate identical key pair for same passphrase + username combination
- System SHALL complete key derivation within 500ms (including Argon2id computation)
- System SHALL require both passphrase AND username for derivation
- System SHALL NOT store derived keys - they are generated on-demand

### 2.2 Content Signing
- System SHALL compute SHA-256 hash of content before signing
- System SHALL sign content hash using Ed25519 private key
- System SHALL produce deterministic signatures (same input = same signature)
- System SHALL complete signing operation within 50ms for content up to 10KB
- System SHALL NOT sign content directly, only its hash

### 2.3 Signature Verification
- System SHALL verify Ed25519 signatures using public key
- System SHALL accept content, signature, and public key as inputs
- System SHALL return boolean result: valid or invalid
- System SHALL complete verification within 50ms
- System SHALL NOT throw exceptions for invalid signatures (return false instead)

### 2.4 Passphrase Requirements
- System SHALL require minimum passphrase length of 12 characters
- System SHALL provide entropy estimation for passphrase strength
- System SHALL warn users about weak passphrases
- System SHALL require passphrase confirmation during initial setup
- System SHALL clearly communicate that passphrase loss means permanent identity loss
- System SHALL NOT store passphrases in any form
- System SHALL NOT transmit passphrases over network

### 2.5 Key Representation
- System SHALL encode public keys as base64 strings for display
- System SHALL encode signatures as base64 strings for display
- System SHALL accept base64-encoded inputs for verification
- System SHALL validate key and signature formats before use

## 3. User Interface Requirements

### 3.1 Identity Management Display
- System SHALL display current public key when passphrase + username are provided
- System SHALL indicate when no identity is active (no passphrase entered)
- System SHALL provide interface to enter passphrase and username
- System SHALL show truncated public key with copy button (e.g., "ed25519:AbCd...XyZ")
- System SHALL clear passphrase from UI after key derivation
- System SHALL provide "Lock Identity" button to clear active session

### 3.2 Content Signing Interface
- System SHALL provide text area for content input
- System SHALL provide "Sign Content" button
- System SHALL display generated signature when signing completes
- System SHALL show signature status indicator (signed/unsigned)
- System SHALL provide copy button for signature

### 3.3 Signature Verification Interface
- System SHALL provide input for content to verify
- System SHALL provide input for signature
- System SHALL provide input for public key
- System SHALL display verification result (valid/invalid)
- System SHALL use clear visual indicators (checkmark/X, green/red)

### 3.4 Status Indicators
- System SHALL show "No identity active" when no passphrase has been entered
- System SHALL show "Identity active" when passphrase + username are in use
- System SHALL show "Signed" badge for signed content
- System SHALL show "Unsigned" badge for unsigned content
- System SHALL show "Signature Valid" for verified content
- System SHALL show "Signature Invalid" for failed verification

### 3.5 Passphrase Entry Interface
- System SHALL provide secure passphrase input field (masked by default)
- System SHALL provide username input field
- System SHALL provide option to show/hide passphrase
- System SHALL display passphrase strength indicator
- System SHALL show clear warning about passphrase importance
- System SHALL require confirmation of understanding that passphrase loss is permanent

## 4. Technical Constraints

### 4.1 Algorithm Requirements
- System SHALL use Ed25519 algorithm exclusively for signatures
- System SHALL use Argon2id for key derivation function (KDF)
- System SHALL use SHA-256 for content hashing
- System SHALL use Web Crypto API where available
- System SHALL use established cryptographic libraries (e.g., libsodium.js) for Argon2id
- System SHALL NOT implement custom cryptographic primitives

### 4.2 Data Size Limits
- System SHALL support content up to 100KB for signing
- System SHALL support unlimited content size for hashing (streaming)
- System SHALL generate 64-byte signatures (Ed25519 standard)
- System SHALL use 32-byte public keys (Ed25519 standard)
- System SHALL use 64-byte private keys (Ed25519 standard)

### 4.3 Browser Requirements
- System SHALL work in Chrome 90+
- System SHALL work in Firefox 90+
- System SHALL work in Safari 15+
- System SHALL work in Edge 90+
- System SHALL detect and report missing Web Crypto API support

## 5. Security Requirements

### 5.1 Key Security
- System SHALL NOT log private keys or passphrases
- System SHALL NOT display private keys or passphrases in UI
- System SHALL NOT include private keys or passphrases in error messages
- System SHALL clear key material from memory immediately after use
- System SHALL clear passphrase from memory immediately after key derivation
- System SHALL use constant-time comparison for signature verification
- System SHALL NOT store keys in any persistent storage
- System SHALL derive keys fresh for each session requiring them

### 5.2 Passphrase Security
- System SHALL use Argon2id with appropriate parameters (memory: 64MB, iterations: 3, parallelism: 4)
- System SHALL combine passphrase with username as salt for derivation
- System SHALL clear passphrase from all variables after derivation
- System SHALL NOT cache or store derived keys between operations
- System SHALL warn users about passphrase importance before first use
- System SHALL educate users that both passphrase AND username are required

## 6. Error Handling Requirements

### 6.1 User-Facing Errors
- System SHALL display "Key derivation failed" if derivation fails
- System SHALL display "Signing failed" if signature creation fails
- System SHALL display "Invalid signature format" for malformed signatures
- System SHALL display "Invalid public key format" for malformed keys
- System SHALL display "Browser not supported" if required crypto unavailable
- System SHALL display "Passphrase too short" for insufficient passphrase length
- System SHALL display "Username required" when username is missing
- System SHALL display "Passphrase required" when passphrase is missing

### 6.2 Error Recovery
- System SHALL allow retry of failed operations
- System SHALL NOT crash on invalid inputs
- System SHALL sanitize error messages to exclude sensitive data

## 7. MVP Limitations (Accepted)

- No passphrase recovery mechanism (loss is permanent)
- No key backup (keys are deterministic from passphrase)
- No key rotation (changing identity requires new passphrase)
- No revocation mechanism
- No multi-device synchronization (same passphrase works everywhere)
- No hardware key support
- Single key pair only (one identity per passphrase + username)
- No signature timestamps
- No signature metadata
- No batch operations
- Username change requires new identity (produces different keys)

## 8. Success Criteria

The MVP is complete when:
- User can derive Ed25519 key pair from passphrase + username
- User can sign content hash with derived private key
- User can verify signatures with public key
- UI clearly shows signed/unsigned status
- Same passphrase + username produces identical keys across sessions
- User understands that passphrase loss means identity loss
- All operations complete within specified time limits
- No keys are stored anywhere - only derived when needed

---
*End of MVP Requirements*