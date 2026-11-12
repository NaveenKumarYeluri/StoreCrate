# Security Policy

## Supported versions

StoreCrate is a small, fast-moving project. Security fixes are applied to the
latest release on the `main` branch.

| Version | Supported |
|---|---|
| Latest release | Yes |
| Older releases | Best effort |

## Reporting a vulnerability

Please **do not** open a public issue for a security problem.

Use GitHub's private vulnerability reporting:

1. Go to the repository's **Security** tab.
2. Choose **Report a vulnerability**.
3. Describe the issue, the affected version, and reproduction steps.

If private reporting is unavailable, contact the maintainer through the email
listed on their GitHub profile and include "StoreCrate security" in the subject.

Please include:

- the StoreCrate version and Windows build;
- the command or UI action involved;
- the expected and actual behaviour;
- any relevant log output from `%LOCALAPPDATA%\StoreCrate`.

You can expect an acknowledgement within a few days. Please allow reasonable
time for a fix before public disclosure.

## Scope

StoreCrate downloads and registers packages supplied by Microsoft's delivery
service. Windows verifies package signatures during installation. StoreCrate
does not implement license bypasses, DRM circumvention, or package tampering.

Reports about vulnerabilities in Microsoft services or packages should go to
Microsoft, not to this project.
