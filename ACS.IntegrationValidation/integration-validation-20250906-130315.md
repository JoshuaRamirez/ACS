# ACS System Integration Validation Report
=========================================

**Test Run ID**: a68ef28d-def5-4c6a-b03e-39dfcf53aa31
**Test Suite**: ACS Focused Integration Tests
**Duration**: 0.74 seconds
**Overall Status**: ❌ FAILED

## Test Summary
- **Total Tests**: 9
- **Passed**: 8 (88.9%)
- **Failed**: 1

## Test Results
### ✅ DOMAIN_ENTITY_OPERATIONS
**Status**: PASSED
**Details**: Domain entities can be created and basic operations work

### ✅ DOMAIN_ENTITY_RELATIONSHIPS
**Status**: PASSED
**Details**: Entity parent-child relationships work correctly

### ✅ SERVICE_LAYER_REGISTRATION
**Status**: PASSED
**Details**: Service layer components can be registered and resolved

### ✅ SERVICE_LAYER_FUNCTIONALITY
**Status**: PASSED
**Details**: Service layer basic functionality working - entity count: 0

### ❌ DATABASE_INTEGRATION
**Status**: FAILED
**Details**: Database integration failed: The navigation 'User.Metadata' must be configured in 'OnModelCreating' with an explicit name for the target shared-type entity type, or excluded by calling 'EntityTypeBuilder.Ignore'.
**Stack Trace**:
```
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.<ValidatePropertyMapping>g__Validate|7_0(IConventionTypeBase typeBase, <>c__DisplayClass7_0&)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.ValidatePropertyMapping(IModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.Validate(IModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.Infrastructure.RelationalModelValidator.Validate(IModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal.SqlServerModelValidator.Validate(IModel model, IDiagnosticsLogger`1 logger)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelRuntimeInitializer.Initialize(IModel model, Boolean designTime, IDiagnosticsLogger`1 validationLogger)
   at Microsoft.EntityFrameworkCore.Infrastructure.ModelSource.GetModel(DbContext context, ModelCreationDependencies modelCreationDependencies, Boolean designTime)
   at Microsoft.EntityFrameworkCore.Internal.DbContextServices.CreateModel(Boolean designTime)
   at Microsoft.EntityFrameworkCore.Internal.DbContextServices.get_Model()
   at Microsoft.EntityFrameworkCore.Infrastructure.EntityFrameworkServicesBuilder.<>c.<TryAddCoreServices>b__8_4(IServiceProvider p)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitFactory(FactoryCallSite factoryCallSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSiteMain(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitCache(ServiceCallSite callSite, RuntimeResolverContext context, ServiceProviderEngineScope serviceProviderEngine, RuntimeResolverLock lockType)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitScopeCache(ServiceCallSite callSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitConstructor(ConstructorCallSite constructorCallSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSiteMain(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitCache(ServiceCallSite callSite, RuntimeResolverContext context, ServiceProviderEngineScope serviceProviderEngine, RuntimeResolverLock lockType)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitScopeCache(ServiceCallSite callSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitConstructor(ConstructorCallSite constructorCallSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSiteMain(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitCache(ServiceCallSite callSite, RuntimeResolverContext context, ServiceProviderEngineScope serviceProviderEngine, RuntimeResolverLock lockType)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitScopeCache(ServiceCallSite callSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitConstructor(ConstructorCallSite constructorCallSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSiteMain(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitCache(ServiceCallSite callSite, RuntimeResolverContext context, ServiceProviderEngineScope serviceProviderEngine, RuntimeResolverLock lockType)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitScopeCache(ServiceCallSite callSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitConstructor(ConstructorCallSite constructorCallSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSiteMain(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitCache(ServiceCallSite callSite, RuntimeResolverContext context, ServiceProviderEngineScope serviceProviderEngine, RuntimeResolverLock lockType)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitScopeCache(ServiceCallSite callSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitConstructor(ConstructorCallSite constructorCallSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSiteMain(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitCache(ServiceCallSite callSite, RuntimeResolverContext context, ServiceProviderEngineScope serviceProviderEngine, RuntimeResolverLock lockType)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitScopeCache(ServiceCallSite callSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.Resolve(ServiceCallSite callSite, ServiceProviderEngineScope scope)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.DynamicServiceProviderEngine.<>c__DisplayClass2_0.<RealizeService>b__0(ServiceProviderEngineScope scope)
   at Microsoft.Extensions.DependencyInjection.ServiceProvider.GetService(ServiceIdentifier serviceIdentifier, ServiceProviderEngineScope serviceProviderEngineScope)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.ServiceProviderEngineScope.GetService(Type serviceType)
   at Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService(IServiceProvider provider, Type serviceType)
   at Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService[T](IServiceProvider provider)
   at Microsoft.EntityFrameworkCore.DbContext.get_DbContextDependencies()
   at Microsoft.EntityFrameworkCore.DbContext.get_ContextServices()
   at Microsoft.EntityFrameworkCore.DbContext.get_InternalServiceProvider()
   at Microsoft.EntityFrameworkCore.DbContext.Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<System.IServiceProvider>.get_Instance()
   at Microsoft.EntityFrameworkCore.Infrastructure.Internal.InfrastructureExtensions.GetService(IInfrastructure`1 accessor, Type serviceType)
   at Microsoft.EntityFrameworkCore.Infrastructure.Internal.InfrastructureExtensions.GetService[TService](IInfrastructure`1 accessor)
   at Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService[TService](IInfrastructure`1 accessor)
   at Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.get_Dependencies()
   at Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnectAsync(CancellationToken cancellationToken)
   at ACS.IntegrationValidation.FocusedIntegrationTests.TestDatabaseIntegrationAsync() in C:\Source\ACS\ACS.IntegrationValidation\FocusedIntegrationTests.cs:line 197
```

### ✅ OBJECT_CREATION_PERFORMANCE
**Status**: PASSED
**Details**: Created 1000 domain objects in 0ms (target: <100ms)

### ✅ RELATIONSHIP_PERFORMANCE
**Status**: PASSED
**Details**: Created relationships in 0ms, using 55KB memory

### ✅ BUSINESS_RULE_VALIDATION
**Status**: PASSED
**Details**: Basic business rules and validation working correctly

### ✅ ENTITY_HIERARCHY_VALIDATION
**Status**: PASSED
**Details**: Entity hierarchy and relationship validation working

## Performance Metrics
- **business_rules_tested**: 3
- **domain_entities_tested**: 3
- **domain_relationships_tested**: 3
- **initial_entity_count**: 0
- **memory_usage_kb**: 55
- **object_creation_time_ms**: 0
- **relationship_creation_time_ms**: 0

## Production Readiness Assessment
### ⚠️ **CAUTION**
System has minor issues that should be addressed before production.

---
*Report generated on 2025-09-06 13:03:15 UTC*
