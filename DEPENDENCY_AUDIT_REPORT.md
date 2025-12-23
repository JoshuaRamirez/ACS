# ACS Dependency Audit Report

**Generated:** December 23, 2025
**Target Framework:** .NET 9.0

---

## Executive Summary

This audit identified **4 critical security issues**, **3 deprecated packages**, **18+ outdated packages**, and several opportunities for consolidation across 17 project files. Immediate attention is required for security vulnerabilities.

---

## 🚨 CRITICAL: Security Vulnerabilities

### 1. System.Data.SqlClient 4.8.6 - CVE-2024-0056

**Location:** `ACS.Service/ACS.Service.csproj:31`

**Severity:** HIGH
**Vulnerability:** Adversary-in-the-Middle (AiTM) attack vulnerability allowing credential theft even over TLS connections.

**Details:** The `ConsumePreLoginHandshake()` method of the `TdsParser` class mishandles encryption options between clients and database servers.

**Recommendation:**
Migrate to `Microsoft.Data.SqlClient` which is the actively maintained replacement:

```xml
<!-- REMOVE -->
<PackageReference Include="System.Data.SqlClient" Version="4.8.6" />

<!-- ADD -->
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
```

**Note:** Code changes required - namespace change from `System.Data.SqlClient` to `Microsoft.Data.SqlClient`.

---

### 2. Microsoft.AspNetCore.Http 2.2.0 / 2.2.2 - MULTIPLE CVEs

**Location:** `ACS.Infrastructure/ACS.Infrastructure.csproj:55-56`

**Severity:** HIGH
**Status:** END-OF-LIFE (since December 23, 2019)

**Known Vulnerabilities:**
- Denial of Service (DoS) vulnerabilities
- Elevation of Privilege vulnerability
- Spoofing vulnerability

**Recommendation:**
Remove these legacy packages entirely. For .NET 9 projects, these abstractions are included in the framework:

```xml
<!-- REMOVE COMPLETELY - These are EOL packages -->
<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.2.0" />
<PackageReference Include="Microsoft.AspNetCore.Http" Version="2.2.2" />
```

The modern equivalents are automatically included when targeting `net9.0` with `Microsoft.NET.Sdk.Web`.

---

### 3. Moq Package - Privacy Concern (Resolved but Caution Advised)

**Locations:**
- `ACS.Service.Tests.Unit/ACS.Service.Tests.Unit.csproj:17` - Version 4.20.70
- `ACS.WebApi.Tests.Performance/ACS.WebApi.Tests.Performance.csproj:21` - Version 4.20.69
- `ACS.VerticalHost.Tests/ACS.VerticalHost.Tests.csproj:16` - Version 4.20.70
- `TestRunner/TestRunner.csproj:13` - Version 4.18.4

**Issue:** Versions 4.20.0 and 4.20.1 included SponsorLink which exfiltrated developer email addresses to external servers (GDPR violation). While 4.20.2+ removed this, the incident raised trust concerns.

**Recommendation:**
1. Standardize on 4.20.70 across all projects
2. Consider migrating to NSubstitute as a more trusted alternative:

```xml
<PackageReference Include="NSubstitute" Version="5.3.0" />
```

---

## ⚠️ Deprecated Packages

### 1. OpenTelemetry.Exporter.Jaeger 1.5.1

**Location:** `ACS.Infrastructure/ACS.Infrastructure.csproj:35`

**Status:** DEPRECATED (July 2023)

**Recommendation:**
Jaeger now natively supports OTLP. Replace with:

```xml
<!-- REMOVE -->
<PackageReference Include="OpenTelemetry.Exporter.Jaeger" Version="1.5.1" />

<!-- KEEP (already present) -->
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
```

Configure Jaeger to receive OTLP data instead of using the deprecated native format.

---

### 2. Polly.Extensions.Http 3.0.0

**Location:** `ACS.Infrastructure/ACS.Infrastructure.csproj:47`

**Status:** DEPRECATED

**Recommendation:**
Replace with the modern resilience package:

```xml
<!-- REMOVE -->
<PackageReference Include="Polly.Extensions.Http" Version="3.0.0" />

<!-- ADD -->
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.0.0" />
```

This integrates better with Polly v8 and provides standardized HTTP resilience patterns.

---

### 3. AspNetCore.HealthChecks.SqlServer 8.0.2

**Location:** `ACS.VerticalHost/ACS.VerticalHost.csproj:17`

**Issue:** Version mismatch with .NET 9 target framework.

**Recommendation:**
Update to the version matching your target framework:

```xml
<PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />
```

---

## 📦 Outdated Packages

### Major Version Updates Available

| Package | Current | Latest | Location |
|---------|---------|--------|----------|
| Swashbuckle.AspNetCore | 7.2.0 | 10.0.1 | ACS.WebApi |
| Swashbuckle.AspNetCore.Annotations | 7.2.0 | 10.1.0 | ACS.WebApi |
| Google.Protobuf | 3.28.3 | 3.33.1 | ACS.Core |
| protobuf-net | 3.2.30 | 3.2.56 | ACS.Infrastructure |
| Azure.Identity | 1.12.0 | 1.13.1 | ACS.Infrastructure |
| Azure.Security.KeyVault.Secrets | 4.6.0 | 4.7.0 | ACS.Infrastructure |
| Azure.Security.KeyVault.Certificates | 4.6.0 | 4.7.0 | ACS.Infrastructure |
| StackExchange.Redis | 2.8.16 | 2.8.22 | ACS.Infrastructure |
| FluentAssertions | 6.12.0 | 7.0.0 | Multiple test projects |
| HtmlAgilityPack | 1.11.54 | 1.11.72 | ACS.WebApi.Tests.Security |
| NBomber | 5.4.0 | 5.8.0 | ACS.WebApi.Tests.Performance |

### Inconsistent Package Versions Across Projects

These packages have different versions in different test projects, which should be standardized:

| Package | Versions Found | Recommended |
|---------|----------------|-------------|
| Microsoft.NET.Test.Sdk | 17.6.0, 17.8.0, 17.12.0 | 17.12.0 |
| MSTest.TestAdapter | 3.0.4, 3.1.1, 3.6.3 | 3.6.4 (use MSTest metapackage) |
| MSTest.TestFramework | 3.0.4, 3.1.1, 3.6.3 | 3.6.4 (use MSTest metapackage) |
| coverlet.collector | 6.0.0, 6.0.2 | 6.0.2 |
| Moq | 4.18.4, 4.20.69, 4.20.70 | 4.20.70 |
| BenchmarkDotNet | 0.13.12, 0.14.0 | 0.14.0 |

---

## 🧪 Beta/Prerelease Packages in Production Code

The following prerelease packages are used in main production projects (not test projects):

| Package | Version | Location | Concern |
|---------|---------|----------|---------|
| OpenTelemetry.Instrumentation.GrpcNetClient | 1.9.0-beta.1 | ACS.WebApi, ACS.Infrastructure, ACS.VerticalHost | Beta in production |
| OpenTelemetry.Exporter.Prometheus.HttpListener | 1.9.0-beta.2 | ACS.Infrastructure | Beta in production |
| OpenTelemetry.Instrumentation.EntityFrameworkCore | 1.0.0-beta.12 | ACS.Infrastructure, ACS.VerticalHost | Long-standing beta |
| OpenTelemetry.Instrumentation.Process | 0.5.0-beta.6 | ACS.Infrastructure | Alpha/Beta in production |
| OpenTelemetry.Instrumentation.EventCounters | 1.5.1-alpha.1 | ACS.Infrastructure | Alpha in production |

**Recommendation:**
Consider if these telemetry features are essential. If stability is critical, wait for stable releases or implement custom instrumentation.

---

## 🔄 Duplicate & Redundant Dependencies

### 1. Dual Protobuf Implementations

**Locations:**
- `ACS.Core`: `Google.Protobuf` + `Grpc.Tools`
- `ACS.Infrastructure`: `protobuf-net`

**Issue:** Two different protobuf serialization libraries are in use.

**Recommendation:**
Standardize on one approach:
- Use `Google.Protobuf` for gRPC (contract-first with .proto files)
- Remove `protobuf-net` unless code-first serialization is specifically needed

---

### 2. Dual HTML Parsing Libraries

**Location:** `ACS.WebApi.Tests.Security/ACS.WebApi.Tests.Security.csproj`

```xml
<PackageReference Include="AngleSharp" Version="0.17.1" />
<PackageReference Include="HtmlAgilityPack" Version="1.11.54" />
```

**Recommendation:**
Choose one library. AngleSharp is more modern with CSS selector support; HtmlAgilityPack is simpler for XPath queries.

---

### 3. Entity Framework Core Duplication

EF Core packages are referenced in multiple projects:
- ACS.Service
- ACS.Infrastructure
- ACS.VerticalHost

**Recommendation:**
Consider centralizing EF Core in `ACS.Infrastructure` only and having other projects reference it through project references.

---

## 📉 Bloat Analysis

### 1. Roslyn Compiler Packages

**Location:** `ACS.Infrastructure/ACS.Infrastructure.csproj:48-51` and `ACS.VerticalHost/ACS.VerticalHost.csproj:24-28`

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.11.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.Common" Version="4.11.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.11.0" />
```

**Impact:** ~50+ MB of dependencies

**Question:** Is runtime code analysis/generation actually needed? These packages are typically for:
- IDE tooling
- Code generators
- Static analysis tools

If not needed at runtime, remove them to significantly reduce deployment size.

---

### 2. System.Threading.Channels Version Mismatch

**Locations:** Multiple projects using version 8.0.0 on .NET 9.0

**Recommendation:**
Update to match the target framework:

```xml
<PackageReference Include="System.Threading.Channels" Version="9.0.0" />
```

---

## ✅ Recommended Actions

### Immediate (Security)

1. **Replace System.Data.SqlClient** with Microsoft.Data.SqlClient
2. **Remove Microsoft.AspNetCore.Http 2.2.x packages** (EOL)
3. **Standardize Moq versions** to 4.20.70 or migrate to NSubstitute

### Short-term (Deprecated/Outdated)

4. **Remove OpenTelemetry.Exporter.Jaeger** - use OTLP instead
5. **Replace Polly.Extensions.Http** with Microsoft.Extensions.Http.Resilience
6. **Update Swashbuckle** from 7.2.0 to 10.0.1
7. **Standardize test package versions** across all test projects

### Medium-term (Optimization)

8. **Evaluate Roslyn packages** - remove if not needed at runtime
9. **Consolidate HTML parsing** to single library
10. **Implement Central Package Management** using Directory.Packages.props
11. **Evaluate beta OpenTelemetry packages** for production readiness

---

## 💡 Suggested: Central Package Management

Create `Directory.Packages.props` in solution root to centralize version management:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />

    <!-- Testing -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="MSTest" Version="3.6.4" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
    <PackageVersion Include="Moq" Version="4.20.70" />

    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    <!-- ... etc ... -->
  </ItemGroup>
</Project>
```

Then in csproj files, omit the Version attribute:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" />
```

---

## Sources

- [CVE-2024-0056 - System.Data.SqlClient Vulnerability](https://github.com/dotnet/announcements/issues/292)
- [Microsoft Security Advisory for SqlClient](https://techcommunity.microsoft.com/blog/sqlserver/released-security-updates-for-microsoft-data-sqlclient-and-system-data-sqlclient/4024264)
- [ASP.NET Core 2.2 CVE Details](https://www.cvedetails.com/version/1687085/Microsoft-Asp.net-Core-2.2.0.html)
- [OpenTelemetry Jaeger Exporter Deprecation](https://opentelemetry.io/blog/2023/jaeger-exporter-collector-migration/)
- [Polly.Extensions.Http Deprecation](https://github.com/dotnet/aspnetcore/issues/57209)
- [Moq SponsorLink Controversy](https://snyk.io/blog/moq-package-exfiltrates-user-emails/)
- [Swashbuckle.AspNetCore NuGet](https://www.nuget.org/packages/swashbuckle.aspnetcore)
