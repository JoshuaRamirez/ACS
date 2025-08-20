namespace ACS.Infrastructure.Monitoring;

/// <summary>
/// Application-specific metrics definitions
/// </summary>
public static class ApplicationMetrics
{
    // API Metrics
    public static class Api
    {
        public const string RequestCount = "api.request.count";
        public const string RequestDuration = "api.request.duration";
        public const string RequestErrors = "api.request.errors";
        public const string RequestRate = "api.request.rate";
        public const string ResponseSize = "api.response.size";
        public const string ActiveRequests = "api.requests.active";
    }

    // Authentication Metrics
    public static class Auth
    {
        public const string LoginAttempts = "auth.login.attempts";
        public const string LoginSuccess = "auth.login.success";
        public const string LoginFailure = "auth.login.failure";
        public const string TokensIssued = "auth.tokens.issued";
        public const string TokensExpired = "auth.tokens.expired";
        public const string TokensRevoked = "auth.tokens.revoked";
        public const string ActiveSessions = "auth.sessions.active";
    }

    // Database Metrics
    public static class Database
    {
        public const string QueryCount = "db.query.count";
        public const string QueryDuration = "db.query.duration";
        public const string ConnectionsActive = "db.connections.active";
        public const string ConnectionsIdle = "db.connections.idle";
        public const string ConnectionErrors = "db.connection.errors";
        public const string TransactionCount = "db.transaction.count";
        public const string TransactionDuration = "db.transaction.duration";
        public const string DeadlockCount = "db.deadlock.count";
    }

    // Cache Metrics
    public static class Cache
    {
        public const string Hits = "cache.hits";
        public const string Misses = "cache.misses";
        public const string HitRatio = "cache.hit_ratio";
        public const string Evictions = "cache.evictions";
        public const string Size = "cache.size";
        public const string ItemCount = "cache.item_count";
    }

    // Business Metrics
    public static class Business
    {
        public const string UsersCreated = "business.users.created";
        public const string UsersDeleted = "business.users.deleted";
        public const string GroupsCreated = "business.groups.created";
        public const string RolesAssigned = "business.roles.assigned";
        public const string PermissionsGranted = "business.permissions.granted";
        public const string PermissionsDenied = "business.permissions.denied";
        public const string PermissionChecks = "business.permission.checks";
        public const string ResourcesAccessed = "business.resources.accessed";
    }

    // Performance Metrics
    public static class Performance
    {
        public const string CpuUsage = "performance.cpu.usage";
        public const string MemoryUsage = "performance.memory.usage";
        public const string GcCollections = "performance.gc.collections";
        public const string ThreadCount = "performance.threads.count";
        public const string RequestQueueLength = "performance.request_queue.length";
        public const string ResponseTime = "performance.response.time";
    }

    // Error Metrics
    public static class Errors
    {
        public const string TotalErrors = "errors.total";
        public const string ValidationErrors = "errors.validation";
        public const string AuthorizationErrors = "errors.authorization";
        public const string DatabaseErrors = "errors.database";
        public const string ExternalServiceErrors = "errors.external_service";
        public const string UnhandledExceptions = "errors.unhandled";
    }

    // gRPC Metrics
    public static class Grpc
    {
        public const string CallsStarted = "grpc.calls.started";
        public const string CallsCompleted = "grpc.calls.completed";
        public const string CallsFailed = "grpc.calls.failed";
        public const string StreamMessagesReceived = "grpc.stream.messages.received";
        public const string StreamMessagesSent = "grpc.stream.messages.sent";
        public const string CallDuration = "grpc.call.duration";
    }

    // Rate Limiting Metrics
    public static class RateLimiting
    {
        public const string RequestsAllowed = "ratelimit.requests.allowed";
        public const string RequestsThrottled = "ratelimit.requests.throttled";
        public const string ActiveBuckets = "ratelimit.buckets.active";
        public const string BucketOverflows = "ratelimit.bucket.overflows";
    }

    // Tenant Metrics
    public static class Tenant
    {
        public const string ActiveTenants = "tenant.active.count";
        public const string TenantRequests = "tenant.requests";
        public const string TenantStorage = "tenant.storage.bytes";
        public const string TenantUsers = "tenant.users.count";
    }
}

/// <summary>
/// Metric tags for consistent tagging
/// </summary>
public static class MetricTags
{
    public const string Endpoint = "endpoint";
    public const string Method = "method";
    public const string StatusCode = "status_code";
    public const string ErrorType = "error_type";
    public const string TenantId = "tenant_id";
    public const string UserId = "user_id";
    public const string Operation = "operation";
    public const string Resource = "resource";
    public const string Action = "action";
    public const string Result = "result";
    public const string Service = "service";
    public const string Component = "component";
    public const string Environment = "environment";
    public const string Version = "version";
    public const string Host = "host";
}