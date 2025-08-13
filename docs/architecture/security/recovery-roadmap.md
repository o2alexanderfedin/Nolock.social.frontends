# Recovery Mechanism Roadmap

## Executive Summary

NoLock.social launches without traditional recovery mechanisms by design. This document outlines the rationale for this decision and the phased approach for introducing recovery features post-launch without compromising existing users or system integrity.

## MVP Launch Decision

### Decision Statement
NoLock.social v1.0 launches with deterministic key derivation as the sole recovery mechanism, deliberately excluding mnemonic phrases and social recovery.

### Rationale

1. **Security First**: Eliminating recovery vectors removes the most common attack surfaces in self-custody systems
2. **User Education**: Forces users to understand the importance of password security from day one
3. **Clean Architecture**: Allows us to validate core cryptographic primitives without recovery complexity
4. **Market Differentiation**: Positions NoLock as a high-security option for users who prioritize control

### Risk Acceptance
- Users who lose their password cannot recover their identity
- This is communicated clearly during onboarding
- Target audience: security-conscious early adopters who understand the trade-offs

## Core Recovery Mechanism: Deterministic Derivation

The foundation that enables future recovery features without migration:

```
Master Key = Argon2id(password, username)
├── Identity Keys = HKDF(Master Key, "identity")
├── Encryption Keys = HKDF(Master Key, "encryption")
└── Future Recovery Keys = HKDF(Master Key, "recovery")
```

This deterministic approach means:
- Any recovery method ultimately reconstructs the Master Key
- No database migrations needed when adding recovery features
- Existing users automatically gain access to new recovery options

## Implementation Phases

### Phase 1: Mnemonic Export (Months 2-3)

**Objective**: Allow users to export a BIP39 mnemonic that can recreate their Master Key

**Technical Implementation**:
```
1. Generate entropy from Master Key: 
   entropy = HKDF(Master Key, "mnemonic-entropy", 32 bytes)

2. Create mnemonic:
   mnemonic = BIP39.encode(entropy)

3. Recovery process:
   entropy = BIP39.decode(mnemonic)
   Master Key = HKDF(entropy, username || "recovery-v1")
```

**User Experience**:
- Optional feature accessed through Settings → Security
- Requires password confirmation to generate
- One-time display with strong warnings about secure storage
- Can be regenerated (produces same mnemonic due to deterministic derivation)

**Security Considerations**:
- Mnemonic is derived FROM the Master Key, not vice versa
- Username still required for recovery (2-factor approach)
- Rate limiting on recovery attempts

### Phase 2: Social Recovery (Months 4-6)

**Objective**: Enable account recovery through trusted contacts using Shamir's Secret Sharing

**Technical Implementation**:
```
1. Setup:
   recovery_key = HKDF(Master Key, "social-recovery")
   shares = Shamir.split(recovery_key, threshold=k, total=n)
   encrypted_shares = shares.map(s => encrypt(s, guardian_pubkey))

2. Recovery:
   decrypted_shares = guardians.decrypt(encrypted_shares)
   recovery_key = Shamir.combine(decrypted_shares, threshold=k)
   Master Key = derive_from_recovery(recovery_key, username)
```

**Guardian System**:
- Minimum 3 guardians, threshold configurable (default 2-of-3)
- Guardians store encrypted shares off-chain
- Recovery requires guardian signatures + username
- Time-locked recovery (48-hour delay) with notification

**User Experience**:
- Progressive disclosure: hidden until user has established connections
- Guardian invitations sent through secure channels
- Recovery initiated through web interface with guardian coordination

### Phase 3: Hardware Security Module Support (Months 7-9)

**Objective**: Integration with hardware wallets for key backup

**Technical Approach**:
- Master Key derivation path stored in HSM
- HSM signs recovery proofs without exposing keys
- Compatible with existing Ledger/Trezor devices

## Backwards Compatibility Guarantees

### Immutable Guarantees
1. **No Migration Required**: All recovery methods derive from the existing Master Key
2. **Opt-in Adoption**: Users choose if/when to enable recovery features
3. **Password Remains Primary**: Recovery methods supplement, never replace, password auth

### Version Compatibility Matrix

| User Created | Mnemonic Export | Social Recovery | HSM Support |
|--------------|-----------------|-----------------|-------------|
| v1.0 (MVP)   | ✅ Compatible   | ✅ Compatible   | ✅ Compatible |
| v1.1+        | ✅ Native       | ✅ Native       | ✅ Native     |

### Key Derivation Stability
```javascript
// This derivation logic is immutable after v1.0
const deriveMasterKey = async (password, username) => {
  const salt = await sha256(username.toLowerCase());
  return argon2id(password, salt, {
    memory: 64 * 1024,
    iterations: 3,
    parallelism: 1
  });
};
```

## Implementation Timeline

### Month 1 (MVP Launch)
- ✅ Deterministic key derivation
- ✅ Clear messaging about no-recovery design
- ✅ Password strength enforcement

### Months 2-3 (Mnemonic Export)
- [ ] BIP39 integration
- [ ] Secure display component
- [ ] Export audit logging
- [ ] Recovery interface

### Months 4-6 (Social Recovery)
- [ ] Shamir secret sharing implementation
- [ ] Guardian management interface
- [ ] Encrypted share distribution
- [ ] Recovery coordination protocol
- [ ] Time-lock mechanism

### Months 7-9 (HSM Support)
- [ ] Hardware wallet SDK integration
- [ ] Signing protocol implementation
- [ ] Recovery proof system

## Risk Assessment

### Current Risks (MVP)
| Risk | Severity | Mitigation |
|------|----------|------------|
| Password loss | Critical | Clear warnings, password strength requirements |
| User confusion | Medium | Extensive onboarding, documentation |
| Market adoption | Medium | Target security-conscious early adopters |

### Future Risks (With Recovery)
| Risk | Severity | Mitigation |
|------|----------|------------|
| Mnemonic theft | High | Encryption, secure display, user education |
| Social engineering | High | Time-locks, multi-factor verification |
| Guardian collusion | Medium | Threshold configuration, guardian diversity |
| Backwards compatibility | Low | Immutable derivation, extensive testing |

## Security Audit Requirements

Each phase requires independent security review:

1. **Phase 1 Audit Focus**:
   - Entropy generation quality
   - BIP39 implementation correctness
   - Side-channel resistance in mnemonic display

2. **Phase 2 Audit Focus**:
   - Shamir implementation
   - Guardian authentication protocol
   - Time-lock bypass resistance

3. **Phase 3 Audit Focus**:
   - HSM communication security
   - Key derivation path standardization
   - Hardware vulnerability assessment

## Success Metrics

### Phase 1 Success Criteria
- < 0.1% of users report recovery issues
- 0 security incidents related to mnemonic exposure
- > 30% of active users generate mnemonic backup

### Phase 2 Success Criteria
- > 20% of eligible users configure guardians
- < 48 hour average recovery time
- 0 unauthorized recovery attempts

### Phase 3 Success Criteria
- Support for top 3 hardware wallets
- < 5 minute setup time
- Seamless integration with existing auth flow

## Conclusion

This phased approach allows NoLock.social to launch with maximum security while providing a clear path to user-friendly recovery options. The deterministic key derivation architecture ensures that all future recovery mechanisms can be added without breaking changes or migrations, protecting early adopters while enabling mainstream adoption.

The deliberate absence of recovery in MVP is not a limitation but a security feature that establishes trust with our initial user base while we perfect the recovery mechanisms that will enable broader adoption.