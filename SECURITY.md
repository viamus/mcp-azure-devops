# Security Policy

## Supported Versions

This project is currently in its **initial public release (v1.0)**.

At this stage:

- Only the **latest released version** is supported for security fixes.
- No long-term support (LTS) versions are available yet.
- Security fixes will be applied to the `main` branch and released as patch versions when applicable.

| Version | Supported |
|--------|-----------|
| v1.x   | ✅ Yes    |
| < v1.0 | ❌ No    |

---

## Reporting a Vulnerability

If you discover a security vulnerability, **please do not open a public issue**.

Instead, report it privately using one of the following methods:

- **Email:** <ADD_SECURITY_CONTACT_EMAIL>
- **Private message or secure channel:** <ADD_ALTERNATIVE_CONTACT>

Please include as much detail as possible to help us understand and reproduce the issue:

- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Affected versions
- Any proof-of-concept (if available)

---

## Scope

The security policy applies to:

- The MCP Azure DevOps Server codebase
- MCP tools exposed by the server
- Configuration handling (environment variables, secrets)
- HTTP endpoints exposed by the server

Out of scope (for v1.0):

- Third-party services (Azure DevOps, Docker, MCP clients)
- Misconfigured client environments
- Compromised Personal Access Tokens (PATs)
- Denial-of-service caused by external infrastructure or networks

---

## Security Considerations (v1.0)

As an early-stage project, the following considerations apply:

- Authentication is based on **Azure DevOps Personal Access Tokens (PAT)**
- The server provides **read-only access** to Git Repositories and **read-write access** to Work Items
- Git file content retrieval may expose sensitive data if repositories contain secrets
- No secrets are persisted; all credentials are provided via environment variables
- Users are responsible for securing their runtime environment
- Logging avoids sensitive data by design, but misconfiguration may expose information

---

## Vulnerability Handling Process

When a vulnerability is reported:

1. The maintainers will acknowledge the report.
2. The issue will be validated and assessed for impact.
3. A fix will be developed and tested.
4. A patch release will be published if necessary.
5. The reporter may be credited (if desired).

Timelines are **best-effort** and may vary depending on severity and available resources.

---

## Disclosure Policy

We follow a **responsible disclosure** approach:

- Vulnerabilities should be reported privately.
- Public disclosure should only occur **after a fix is released** or explicitly agreed upon.
- Coordinated disclosure helps protect users and the ecosystem.

---

## Hardening Recommendations

For users running this server in production-like environments:

- Use **minimal-scope PATs**:
  - Work Items: Read (or Read & Write if comments are needed)
  - Code: Read (for Git repository access)
- Never commit `.env` files or secrets
- Be aware that Git file content retrieval may expose sensitive files in repositories
- Restrict network access to the MCP server
- Monitor logs and usage patterns
- Rotate PATs periodically

---

## Security Updates

Security-related fixes will be documented in:

- GitHub Releases
- Release Notes (when applicable)

---

## Acknowledgements

We appreciate responsible security researchers and contributors who help
improve the safety and reliability of this project.

Thank you for helping keep MCP Azure DevOps Server secure.
