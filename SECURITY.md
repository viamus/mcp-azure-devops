# Security Policy

## Supported Versions

Security support follows the latest released version:

- Only the **latest released version** is supported for security fixes.
- No long-term support (LTS) versions are available yet.
- Security fixes will be applied to the `main` branch and released as patch versions when applicable.

| Version | Supported |
|---------|-----------|
| Latest released version | Yes |
| Older releases | No |

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

- The shared Core assembly and both HTTP and STDIO host codebases
- MCP tools exposed by either transport
- Configuration handling (environment variables, settings files, credentials)
- HTTP endpoints and STDIO protocol/process handling

Out of scope:

- Third-party services (Azure DevOps, Docker, MCP clients)
- Misconfigured client environments
- Compromised Personal Access Tokens (PATs)
- Denial-of-service caused by external infrastructure or networks

---

## Security Considerations

- Both hosts authenticate Azure DevOps operations using **Personal Access Tokens (PATs)**; use HTTPS organization URLs for the upstream credential connection.
- Work item tools support reads and writes. Git file browsing and Wiki tools are read-only, while pull request tools can create/update PRs, threads, and comments. Access is governed by the PAT and the user's Azure DevOps permissions.
- Git files, work item attachments, Wiki pages, and build logs may expose sensitive content to an authorized MCP client.
- Credentials can be supplied through environment variables, settings files, or MCP client process configuration. These files can persist secrets; keep real credentials outside version control and restrict access to private configuration.
- HTTP API key authentication is optional and disabled by default. When enabled, it accepts `X-API-Key` or `Authorization: Bearer`, while `/health` remains accessible. This API key is separate from the Azure DevOps PAT.
- STDIO has no network listener and does not use HTTP API keys. Secure access to the local account, process environment, and MCP client configuration.
- The shared tool error handler excludes raw exception text, credentials, and stack traces from classified error responses. Its diagnostic logs record tool name, exception type, and error code; upstream resource content can still contain sensitive information.
- Initialization, tool discovery, and HTTP health checks do not validate PAT access. Diagnose authentication and permissions using an actual Azure DevOps operation.

See the [STDIO and error handling guide](docs/stdio-and-error-handling.md) for configuration and PAT replacement steps.

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
  - Work Items: Read for queries/history/attachments, or Read & Write for creation, updates, comments, and relation linking
  - Code: Read for Git queries, or Read & Write for pull request mutations
  - Wiki: Read (`vso.wiki`) for Wiki metadata and pages; see the [Wiki API scope reference](https://learn.microsoft.com/en-us/rest/api/azure/devops/wiki/wikis/list?view=azure-devops-rest-7.1#security)
  - Build: Read for pipelines and builds
- Never commit `.env` files, credential-bearing settings, or MCP client secrets
- Be aware that tools may return sensitive resource content to MCP clients
- For HTTP, restrict network access and configure TLS and API key authentication as appropriate for the deployment
- For STDIO, restrict access to the host process and its private configuration; keep runtime logs on stderr and protocol messages on stdout
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
