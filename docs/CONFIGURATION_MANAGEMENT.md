# Configuration Management Guide

## Overview

The ACS system implements a comprehensive configuration management strategy that supports multiple configuration sources, environment-specific settings, validation, and hot-reload capabilities.

## Configuration Sources

### 1. Configuration Files
- **appsettings.json**: Base configuration for all environments
- **appsettings.{Environment}.json**: Environment-specific overrides
- **appsettings.Development.json**: Development environment settings
- **appsettings.Production.json**: Production environment settings

### 2. Environment Variables
The system supports environment variables with automatic mapping:

#### Standard Mappings
```bash
# Database
DATABASE_URL → ConnectionStrings:DefaultConnection
REDIS_URL → ConnectionStrings:Redis

# Authentication
JWT_SECRET → Authentication:Jwt:SecretKey
JWT_ISSUER → Authentication:Jwt:Issuer
JWT_AUDIENCE → Authentication:Jwt:Audience
JWT_EXPIRATION_HOURS → Authentication:Jwt:ExpirationHours

# Azure Key Vault
AZURE_KEY_VAULT_URI → KeyVault:VaultUri
AZURE_CLIENT_ID → KeyVault:ClientId
AZURE_CLIENT_SECRET → KeyVault:ClientSecret
AZURE_TENANT_ID → KeyVault:TenantId

# Monitoring
OTEL_EXPORTER_OTLP_ENDPOINT → OpenTelemetry:OtlpEndpoint
OTEL_SERVICE_NAME → OpenTelemetry:ServiceName
APPINSIGHTS_INSTRUMENTATIONKEY → ApplicationInsights:InstrumentationKey

# Email/Alerting
SMTP_HOST → Alerting:Channels:Email:SmtpHost
SMTP_PORT → Alerting:Channels:Email:SmtpPort
SMTP_USERNAME → Alerting:Channels:Email:Username
SMTP_PASSWORD → Alerting:Channels:Email:Password
SMTP_FROM → Alerting:Channels:Email:FromAddress

# Slack
SLACK_WEBHOOK_URL → Alerting:Channels:Slack:DefaultWebhookUrl
```

#### Custom Prefix Variables
Variables with `ACS_` prefix are automatically mapped:
```bash
ACS_SECTION__SUBSECTION__KEY → Section:Subsection:Key
```

Example:
```bash
ACS_DATABASE__PERFORMANCE__MAXPOOLSIZE=200
# Maps to: Database:Performance:MaxPoolSize
```

### 3. .env Files
Environment-specific .env files are supported:
- `.env.Development`
- `.env.Production`
- `.env.Staging`

Format:
```bash
# Comments are supported
DATABASE_URL=Server=localhost;Database=ACS;
JWT_SECRET=your-secret-key
REDIS_URL=localhost:6379
```

### 4. Azure Key Vault
In production, secrets are loaded from Azure Key Vault:
```json
{
  "KeyVault": {
    "VaultUri": "${AZURE_KEY_VAULT_URI}",
    "ClientId": "${AZURE_CLIENT_ID}",
    "ClientSecret": "${AZURE_CLIENT_SECRET}",
    "TenantId": "${AZURE_TENANT_ID}",
    "LoadAllSecrets": true
  }
}
```

## Configuration Validation

### Startup Validation
The system validates configuration at startup and can be configured to fail fast:

```json
{
  "Configuration": {
    "FailOnValidationError": true
  }
}
```

### Built-in Validators

1. **Required Settings Validator**
   - Validates presence of critical configuration
   - Environment-specific requirements

2. **Connection String Validator**
   - Validates SQL Server connection strings
   - Checks for security best practices

3. **JWT Configuration Validator**
   - Ensures secure key length (minimum 256 bits)
   - Detects weak/default keys in production

4. **Path Configuration Validator**
   - Validates file paths exist
   - Checks write permissions

5. **Email Configuration Validator**
   - Validates SMTP settings
   - Validates email address formats

6. **Rate Limiting Validator**
   - Validates rate limit policies
   - Checks Redis configuration when distributed

7. **Database Configuration Validator**
   - Validates connection pool settings
   - Checks backup and archiving configuration

### Custom Validators
Implement `IConfigurationValidator` to add custom validation:

```csharp
public class CustomValidator : IConfigurationValidator
{
    public ValidationResult Validate(
        IConfiguration configuration, 
        IServiceProvider serviceProvider)
    {
        var result = new ValidationResult { IsValid = true };
        
        // Add validation logic
        var setting = configuration["CustomSetting"];
        if (string.IsNullOrEmpty(setting))
        {
            result.IsValid = false;
            result.Errors.Add("CustomSetting is required");
        }
        
        return result;
    }
}
```

## Hot-Reload Configuration

### Enabling Hot-Reload
Hot-reload is enabled by default in development and disabled in production:

```json
{
  "Configuration": {
    "EnableHotReload": true
  }
}
```

### Monitored Sections
The following configuration sections support hot-reload:
- Authentication:Jwt
- ConnectionStrings
- RateLimit
- FeatureFlags

### Registering Reload Handlers
```csharp
public class MyService
{
    public MyService(IConfigurationHotReloadService hotReload)
    {
        // Register handler for specific options
        hotReload.RegisterReloadHandler<MyOptions>(options =>
        {
            // Handle configuration change
            UpdateSettings(options);
        });
        
        // Register handler for configuration section
        hotReload.RegisterReloadHandler("MySection", config =>
        {
            // Handle section change
            ProcessConfigChange(config);
        });
    }
}
```

## Environment-Specific Configuration

### Development Environment
```json
{
  "Environment": "Development",
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "Database": {
    "Performance": {
      "EnableSensitiveDataLogging": true,
      "EnableDetailedErrors": true
    }
  },
  "FeatureFlags": {
    "EnableDebugEndpoints": true,
    "EnableTestData": true
  }
}
```

### Production Environment
```json
{
  "Environment": "Production",
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "Security": {
    "RequireHttps": true,
    "Hsts": {
      "MaxAge": 31536000,
      "IncludeSubdomains": true
    }
  },
  "FeatureFlags": {
    "EnableDebugEndpoints": false,
    "EnableTestData": false
  }
}
```

## Variable Substitution

Configuration files support variable substitution:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "${DATABASE_URL:Server=localhost;Database=ACS;}"
  },
  "Authentication": {
    "Jwt": {
      "SecretKey": "${JWT_SECRET}",
      "Issuer": "${JWT_ISSUER:ACS.WebApi}"
    }
  }
}
```

Format: `${VARIABLE_NAME:default_value}`
- `VARIABLE_NAME`: Environment variable to substitute
- `default_value`: Optional default if variable not found

## Best Practices

### 1. Security
- **Never commit secrets** to source control
- Use environment variables or Key Vault for sensitive data
- Rotate secrets regularly
- Use strong, unique keys for JWT and encryption

### 2. Environment Isolation
- Keep development and production configurations separate
- Use environment-specific connection strings
- Disable debug features in production

### 3. Validation
- Always validate configuration at startup
- Fail fast on critical configuration errors
- Log configuration warnings for review

### 4. Documentation
- Document all configuration options
- Provide examples for each environment
- Include default values and constraints

### 5. Hot-Reload
- Use judiciously - not all settings should be reloadable
- Test configuration changes before applying
- Monitor for configuration change events

## Configuration Schema

### Application Settings Structure
```
├── Logging
│   ├── LogLevel
│   └── ApplicationInsights
├── ConnectionStrings
│   ├── DefaultConnection
│   └── Redis
├── Authentication
│   └── Jwt
│       ├── SecretKey
│       ├── Issuer
│       ├── Audience
│       └── ExpirationHours
├── KeyVault
│   ├── VaultUri
│   ├── ClientId
│   ├── ClientSecret
│   └── TenantId
├── RateLimit
│   ├── Enabled
│   ├── DefaultPolicy
│   └── EndpointPolicies
├── Database
│   ├── Backup
│   └── Performance
├── DataArchiving
│   ├── ArchivePath
│   └── Schedule
├── Monitoring
│   ├── EnableMetrics
│   └── EnableDashboards
├── OpenTelemetry
│   ├── Exporter
│   └── OtlpEndpoint
├── Alerting
│   └── Channels
│       ├── Email
│       └── Slack
├── Security
│   ├── RequireHttps
│   └── Hsts
└── FeatureFlags
    ├── EnableDebugEndpoints
    └── EnableTestData
```

## Troubleshooting

### Common Issues

1. **Configuration not loading**
   - Check environment variable names and casing
   - Verify .env file encoding (UTF-8 without BOM)
   - Check file permissions

2. **Validation failures**
   - Review startup logs for specific errors
   - Check required settings are present
   - Verify connection strings are valid

3. **Hot-reload not working**
   - Ensure hot-reload is enabled
   - Check IOptionsMonitor is used (not IOptions)
   - Verify configuration section is monitored

4. **Environment variables not recognized**
   - Check variable prefix (ACS_ for custom)
   - Verify mapping configuration
   - Check for typos in variable names

## Migration Guide

### From hardcoded values to environment variables:

1. Identify sensitive configuration
2. Add environment variable mapping
3. Update configuration files with placeholders
4. Set environment variables in deployment
5. Test configuration loading
6. Remove hardcoded values

### Example migration:
```json
// Before
{
  "Authentication": {
    "Jwt": {
      "SecretKey": "hardcoded-secret-key"
    }
  }
}

// After
{
  "Authentication": {
    "Jwt": {
      "SecretKey": "${JWT_SECRET}"
    }
  }
}
```

Set environment variable:
```bash
export JWT_SECRET="your-secure-secret-key"
```

## Deployment Checklist

- [ ] All secrets moved to environment variables or Key Vault
- [ ] Environment-specific configuration files created
- [ ] Configuration validation enabled
- [ ] Hot-reload disabled in production
- [ ] Connection strings verified for each environment
- [ ] Feature flags set appropriately
- [ ] Logging levels configured
- [ ] Security settings enabled for production
- [ ] Backup paths configured and accessible
- [ ] Monitoring endpoints configured