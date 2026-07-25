# Security Policy

Thank you for helping keep Aura secure.

Security is a core design principle of Aura. Although the project is currently in active development, responsible disclosure of vulnerabilities is greatly appreciated.

---

# Supported Versions

Aura has not yet reached its first public release.

At this stage:

| Version | Supported |
|----------|:---------:|
| Development (main) | ✅ |
| Pre-release builds | ✅ |
| Released versions | N/A |

Security fixes will be prioritized for the latest development branch until stable releases begin.

---

# Reporting a Vulnerability

Please **do not open a public GitHub issue** for security vulnerabilities.

Instead, report vulnerabilities privately by contacting the maintainers.

When reporting a vulnerability, include:

- Description of the issue
- Steps to reproduce
- Potential impact
- Screenshots or proof of concept (if applicable)
- Relevant logs (if available)
- Suggested mitigation (optional)

A clear report helps us investigate and resolve issues more quickly.

---

# Disclosure Policy

Aura follows a responsible disclosure process.

After receiving a report, the maintainers will:

1. Acknowledge receipt of the report.
2. Investigate the issue.
3. Determine severity and impact.
4. Develop and validate a fix.
5. Release the fix.
6. Publicly disclose the issue when appropriate.

Please avoid publicly disclosing vulnerabilities until a fix has been released or coordinated with the maintainers.

---

# Security Philosophy

Security is considered a product feature, not an afterthought.

Aura is designed around several principles:

- Least privilege
- Explicit permissions
- Deterministic behavior
- Minimal background activity
- Clear ownership of sensitive data
- Defense in depth
- Secure defaults
- Privacy by design

Whenever possible, Aura should fail safely rather than unpredictably.

---

# Scope

Security reports may include, but are not limited to:

- Remote code execution
- Privilege escalation
- Plugin sandbox escapes
- Unauthorized file access
- Sensitive data exposure
- Memory corruption
- Authentication or permission bypass
- Dependency vulnerabilities
- Unsafe inter-process communication
- Injection vulnerabilities
- Information disclosure
- Denial of service

Reports involving third-party dependencies should include the affected package and version whenever possible.

---

# Out of Scope

The following are generally considered out of scope unless they directly affect Aura itself:

- Vulnerabilities in third-party software outside Aura's control
- Local-only issues requiring administrator access without increasing privileges
- Social engineering attacks
- Physical device access
- Issues caused by unsupported operating systems or modified environments

---

# Security Goals

Aura aims to:

- Protect user data.
- Preserve user privacy.
- Minimize attack surface.
- Isolate plugins and extensions.
- Avoid unnecessary permissions.
- Keep dependencies minimal.
- Follow secure coding practices.
- Respond quickly to verified security issues.

---

# Future Security Roadmap

As Aura matures, the project plans to introduce:

- Security advisories
- Signed releases
- Reproducible builds
- Automated dependency scanning
- Static security analysis
- Continuous vulnerability monitoring
- Plugin permission validation
- Security-focused CI checks

These capabilities will be introduced progressively as the project evolves.

---

# Acknowledgements

We appreciate responsible security research and welcome reports that help improve Aura for everyone.

Thank you for helping make Aura safer.