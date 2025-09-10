# Defender/Trivy/Grype Scan Output (Sample)

## Summary
- **Critical vulnerabilities:** 0
- **High vulnerabilities:** 1
- **Medium vulnerabilities:** 2
- **Low vulnerabilities:** 5

## High/Critical Findings
- CVE-2025-12345: .NET 8.0 runtime - High - Patch available

## Remediation Checklist
- [ ] Update .NET 8.0 base image to latest patch version
- [ ] Rebuild and rescan image
- [ ] Review and update dependencies in project
- [ ] Apply security patches to OS and libraries
- [ ] Rerun pipeline to verify no high/critical findings

## Notes
- No critical vulnerabilities found. One high vulnerability in base image; patch available.
- No exposed secrets or misconfigurations detected in IaC.
