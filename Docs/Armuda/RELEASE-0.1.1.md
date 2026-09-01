# Armuda 0.1.1 public preview

Armuda `0.1.1` adds a branded ownership card before the start/profile interface.

## Ownership and legal presentation

- A black startup card displays `CREATED BY` and `CyFi Network Corporation` before the application interface becomes available.
- The startup card displays `© 2026 CyFi Network Corporation. All Rights Reserved.`
- The source package includes centralized `LICENSE.md` and `NOTICE.md` ownership notices.
- The Unity company metadata is standardized to `CyFi Network Corporation`.
- Third-party packages and assets remain governed by their respective licenses.

## Distribution and verification

- The Android APK is signed with Armuda's dedicated production Android certificate.
- The Windows executable is intentionally unsigned during the community-preview phase. Windows may warn or block it.
- Download packages only from the official GitHub release page and compare their SHA-256 hashes with `SHA256SUMS.txt`.
- Hash verification detects corruption or a mismatched download; it does not replace authenticated Windows publisher signing.
- Do not disable Windows Security globally to run Armuda.

Detailed verification steps are in [VERIFY_RELEASE.md](VERIFY_RELEASE.md).
