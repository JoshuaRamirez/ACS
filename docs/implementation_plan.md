# ACS Domain and Normalizer Implementation Plan

## Executive Summary

This plan outlines the complete implementation of the ACS (Access Control System) domain and normalizer layers to realize a next-generation, URI-based, hierarchical access control system. The implementation will build upon the proven normalizer pattern foundation and existing project methodology to complete the revolutionary access control architecture while maintaining the established development standards and documentation practices.

## Current State Analysis - MAJOR REVISION

**CRITICAL DISCOVERY**: The codebase is **significantly more advanced** than initially assessed. Previous assumptions were incorrect.

### ✅ **FULLY IMPLEMENTED COMPONENTS**
- **Domain Models**: ALL core models exist - Entity, Group, User, Role, Permission, HttpVerb enum, Scheme enum
- **Data Models**: ALL database models exist - Resource, UriAccess, AuditLog, SchemeType, VerbType, PermissionScheme
- **Normalizers**: 13 complete normalizers including CreateResourceNormalizer, CreateUriAccessNormalizer, CreatePermissionSchemeNormalizer
- **Database Schema**: Complete 10-table schema with constraints, indexes, and seed data
- **Architecture**: Mature Operational Normalization Pattern with comprehensive test coverage (40+ test scenarios)
- **Test Infrastructure**: Extensive unit and integration test projects with established patterns
- **Documentation Standards**: Comprehensive docs with agent-driven maintenance and project tracking

### ❌ **ACTUALLY MISSING COMPONENTS**
- **API Controllers**: NO controllers exist (not even UsersController mentioned in docs)
- **Database Integration**: Normalizers use static collections, not connected to Entity Framework context
- **Service Layer**: UserService exists but is in-memory only, no Group/Role services
- **Domain Business Logic**: Authorization class is empty placeholder, permission evaluation not implemented
- **EF Integration**: ApplicationDbContext exists but not connected to normalizer operations

### 📊 **REVISED COMPLETION STATUS**
- **Domain Layer**: 95% Complete ✅
- **Data Models**: 100% Complete ✅
- **Database Schema**: 100% Complete ✅
- **Normalizer Pattern**: 90% Complete ✅
- **Test Coverage**: 85% Complete ✅
- **API Layer**: 5% Complete ❌
- **Database Integration**: 15% Complete ❌
- **Service Layer**: 25% Complete ❌

## Implementation Methodology

### Project Integration Requirements
This implementation must align with the established project methodology:

- **Agent Persona Adoption**: Each implementation phase will adopt appropriate persona (Lead Developer, QA Engineer, Documentation Specialist)
- **Project File Maintenance**: Update all 4 active project files with progress and task status
- **Documentation Standards**: Update all affected documentation with each change
- **Developer Journal**: Record all architectural decisions and implementation steps
- **Test-First Approach**: Maintain and expand the established comprehensive test coverage
- **Incremental Integration**: Build upon proven normalizer patterns without breaking existing functionality

### Active Project Alignment
- **API Layer Implementation**: Complete database integration and add Group/Role controllers
- **Test Improvement Initiative**: Expand coverage for new domain models and normalizers
- **Documenting Solution**: Update all documentation as features are implemented
- **Code Refactoring**: Clean up and optimize as complexity increases

## COMPLETELY REVISED Implementation Phases

**PHASE 1 ELIMINATED** - All domain models already exist and are complete!

## Phase 1: Database Integration (Highest Priority)
**Timeline**: 1 week  
**Priority**: Critical  
**Persona**: Lead Developer

### 1.1 Connect Normalizers to Entity Framework
**Problem**: Normalizers currently use static collections instead of database context

**Files to Modify**: All 13 normalizer files in `ACS.Service/Delegates/Normalizers/`

**Requirements**:
- Replace static collections with ApplicationDbContext dependency injection
- Maintain existing normalizer method signatures for backward compatibility
- Preserve all existing exception handling patterns
- Ensure existing tests continue to pass (dual-mode support during transition)

**Implementation Strategy**:
```csharp
// Current Pattern:
public static List<Group> Groups { get; set; }

// New Pattern:
public static void Execute(ApplicationDbContext context, int userId, int groupId)
{
    var user = context.Users.SingleOrDefault(x => x.Id == userId)
        ?? throw new InvalidOperationException($"User {userId} not found.");
    // ... rest of logic
}
```

### 1.2 Enhance ApplicationDbContext Integration
**File**: `ACS.Service/Data/ApplicationDbContext.cs`

**Requirements**:
- Ensure all entity relationships are properly configured
- Add any missing DbSet properties
- Configure lazy loading and navigation properties
- Set up proper cascade delete behaviors

### 1.3 Update Service Layer for Database Operations
**File**: `ACS.Service/Services/UserService.cs`

**Requirements**:
- Replace in-memory List<User> with ApplicationDbContext
- Integrate with existing normalizers (now database-connected)
- Maintain existing interface for API compatibility
- Add proper error handling and validation

## Phase 2: API Layer Creation (From Scratch)
**Timeline**: 1 week  
**Priority**: Critical  
**Persona**: Lead Developer

### 2.1 Create API Controllers
**Problem**: NO controllers exist despite documentation references

#### UsersController
**File**: `ACS.WebApi/Controllers/UsersController.cs` (CREATE NEW)

**Requirements**:
- Implement basic CRUD endpoints (GET, POST, PUT, DELETE)
- Integrate with database-connected UserService
- Follow RESTful conventions
- Proper error handling and HTTP status codes
- Input validation and model binding

**Endpoints to Implement**:
```csharp
GET /api/users           // List all users
GET /api/users/{id}      // Get user by ID
POST /api/users          // Create new user
PUT /api/users/{id}      // Update user
DELETE /api/users/{id}   // Delete user
```

#### GroupsController
**File**: `ACS.WebApi/Controllers/GroupsController.cs` (CREATE NEW)

**Requirements**:
- Full CRUD operations for groups
- Hierarchical group operations (add/remove child groups)
- User membership management endpoints
- Role assignment endpoints

#### RolesController
**File**: `ACS.WebApi/Controllers/RolesController.cs` (CREATE NEW)

**Requirements**:
- Full CRUD operations for roles
- User assignment/unassignment endpoints
- Group assignment endpoints
- Permission management endpoints

### 2.2 Create Missing Service Layer Components

#### GroupService & IGroupService
**Files**: `ACS.Service/Services/GroupService.cs`, `ACS.Service/Services/IGroupService.cs` (CREATE NEW)

**Requirements**:
- Database-backed group operations using ApplicationDbContext
- Integration with existing normalizers
- Support for hierarchical group operations
- Cycle prevention validation

#### RoleService & IRoleService  
**Files**: `ACS.Service/Services/RoleService.cs`, `ACS.Service/Services/IRoleService.cs` (CREATE NEW)

**Requirements**:
- Database-backed role operations using ApplicationDbContext
- Integration with existing normalizers
- User assignment/unassignment operations
- Permission aggregation support

### 2.3 Update Dependency Injection
**File**: `ACS.WebApi/Program.cs`

**Requirements**:
- Register ApplicationDbContext with connection string
- Register all service interfaces and implementations
- Configure Entity Framework options
- Set up proper service lifetimes

## Phase 3: Business Logic Implementation  
**Timeline**: 1 week  
**Priority**: High  
**Persona**: Lead Developer

### 3.1 Complete Authorization Domain Model
**File**: `ACS.Service/Domain/Authorization.cs` (Currently Empty Placeholder)

**Requirements**:
- Implement permission evaluation algorithms
- Hierarchical permission resolution through entity relationships  
- Conflict resolution (Deny wins over Grant)
- Support for multiple permission schemes
- Performance-optimized permission checking

**Key Methods to Implement**:
```csharp
public bool HasPermission(Entity entity, string uri, HttpVerb verb)
public List<Permission> ResolvePermissions(Entity entity)
public bool EvaluateAccess(User user, string uri, HttpVerb verb)
```

### 3.2 Enhance Domain Models with Business Logic

#### Entity Domain Model Enhancement
**File**: `ACS.Service/Domain/Entity.cs` (Enhance Existing)

**Requirements**:
- Complete the permission evaluation methods (currently incomplete)
- Optimize the permission aggregation algorithm
- Add caching for computed permissions
- Integrate with Authorization class for complex evaluations

#### Add Domain Resource Model
**File**: `ACS.Service/Domain/Resource.cs` (CREATE NEW - Domain Version)

**Requirements**:
- URI pattern matching and validation
- Resource hierarchy support (/api/users/* patterns) 
- Integration with data model Resource.cs
- Resource-specific permission evaluation

### 3.3 Create Missing Lifecycle Normalizers
**Note**: Most normalizers exist, but missing entity creation/deletion

#### CreateEntityNormalizer (Missing)
**File**: `ACS.Service/Delegates/Normalizers/CreateEntityNormalizer.cs` (CREATE NEW)

#### DeleteEntityNormalizer (Missing)  
**File**: `ACS.Service/Delegates/Normalizers/DeleteEntityNormalizer.cs` (CREATE NEW)

#### AuditLogNormalizer (Missing)
**File**: `ACS.Service/Delegates/Normalizers/AuditLogNormalizer.cs` (CREATE NEW)

**Requirements**:
- Integrate audit logging into all existing normalizers
- Use existing AuditLog data model for persistence

## Phase 4: Testing and Polish
**Timeline**: 1 week  
**Priority**: High  
**Persona**: QA Engineer & Documentation Specialist

### 4.1 Expand Test Coverage for New Components
**Integration with Test Improvement Initiative Project**

#### API Controller Tests
**Files**: `ACS.WebApi.Tests.Integration/*` (Expand Existing)

**Requirements**:
- Test all new controller endpoints (Users, Groups, Roles)
- Test database integration scenarios
- Test error handling and validation
- Test permission-based access control

#### Service Layer Tests  
**Files**: `ACS.Service.Tests.Unit/*` (Expand Existing)

**Requirements**:
- Test database-connected services
- Test normalizer integration with EF context
- Test business logic in Authorization class
- Maintain existing normalizer test patterns

### 4.2 Performance Testing and Optimization

#### Database Performance
**Requirements**:
- Test with realistic data sets (1000+ entities)
- Optimize slow database queries
- Test permission evaluation performance
- Validate hierarchical operations performance

#### Memory and Caching
**Requirements**:
- Test memory usage with large entity hierarchies
- Validate permission calculation caching
- Test concurrent access scenarios

### 4.3 Documentation Updates
**Integration with Documenting Solution Project**

#### Update All Documentation
**Files**: Multiple documentation files

**Requirements**:
- Update API documentation with all new endpoints
- Document database integration approach
- Update user manual with complete functionality
- Document permission evaluation algorithms
- Update architecture documentation with final state

## ELIMINATED PHASES

**Phase 5 & 6**: Merged into Phase 4 since most components already exist.

## REVISED IMPLEMENTATION SUMMARY

### What We're ACTUALLY Building:
1. **Database Integration** (Phase 1) - Connect existing normalizers to Entity Framework
2. **API Layer** (Phase 2) - Create all controllers and missing services from scratch  
3. **Business Logic** (Phase 3) - Complete Authorization class and add missing normalizers
4. **Testing & Polish** (Phase 4) - Expand test coverage and documentation

### What We're NOT Building (Already Exists):
- ❌ Domain models (ALL exist)
- ❌ Data models (ALL exist)  
- ❌ Database schema (100% complete)
- ❌ Core normalizers (13 of 16 exist)
- ❌ Test infrastructure (Established)
- ❌ Documentation framework (Mature)

## Implementation Success Tracking

### Developer Journal Integration
**File**: `DEVELOPER_JOURNAL.md`

Each phase completion must include:
- **Persona adopted** for the implementation work
- **Architectural decisions** made and rationale
- **Test results** and coverage metrics
- **Performance benchmarks** established
- **Documentation updates** completed
- **Lessons learned** and optimization opportunities

### Project File Updates
All active projects must be updated with each phase:

#### API Layer Implementation Project
- Track database integration completion
- Document new controller implementations
- Record service layer enhancements

#### Test Improvement Initiative Project  
- Update test coverage metrics
- Document new testing patterns established
- Track integration test scenarios covered

#### Documenting Solution Project
- Record documentation updates completed
- Track architectural documentation additions
- Note user manual enhancements

#### Code Refactoring Project
- Document performance optimizations
- Record code cleanup and organization improvements
- Track technical debt resolution


## Implementation Dependencies

### REVISED Critical Path Dependencies
1. **Database Integration** (Phase 1) → All subsequent phases depend on this
2. **API Layer Creation** (Phase 2) → Can proceed in parallel after Phase 1 starts
3. **Business Logic** (Phase 3) → Can proceed in parallel with Phase 2
4. **Testing & Polish** (Phase 4) → Depends on completion of Phases 1-3
5. **Documentation maintenance** → Continuous throughout all phases

### REVISED Parallel Development Opportunities
- **API Controllers** can be developed in parallel with service layer (Phase 2)
- **Business logic** can be implemented in parallel with API layer (Phase 3)
- **Unit tests** can be developed alongside each new component
- **Documentation updates** can proceed in parallel with all implementation
- **Integration tests** can be developed after database integration is complete
- **Project file maintenance** occurs continuously throughout all phases

## Success Criteria

### Functional Requirements
- ✅ Complete domain model coverage of database schema with full feature implementation
- ✅ All CRUD operations supported through proven normalizer patterns
- ✅ Hierarchical permission inheritance with established conflict resolution (Deny wins)
- ✅ Comprehensive audit logging integrated into all operations
- ✅ Database integration maintaining existing test compatibility
- ✅ Full API coverage (Users, Groups, Roles controllers) following established patterns

### Technical Requirements  
- ✅ Preserve and enhance operational normalization pattern
- ✅ Maintain existing test coverage while expanding to new functionality
- ✅ Follow established documentation standards and project methodology
- ✅ Integration with existing 4 active projects (API Layer, Test Improvement, Documentation, Code Refactoring)
- ✅ Performance benchmarks with 1000+ entity hierarchies
- ✅ Developer journal maintenance with architectural decision tracking

### Business Requirements
- ✅ Support multiple permission schemes (API, File, Database, UI access)
- ✅ URI-based resource control with granular HTTP verb permissions
- ✅ Complete compliance audit trail with JSON change tracking
- ✅ Extensible architecture for future authorization models
- ✅ Production-ready access control system transcending traditional RBAC limitations

## Risk Mitigation

### Technical Risks
- **Complexity Risk**: Mitigate by building incrementally on proven normalizer foundation
- **Integration Risk**: Maintain existing test compatibility, dual-mode normalizer support
- **Performance Risk**: Leverage existing performance patterns, benchmark early and often
- **Documentation Debt**: Follow established documentation standards, update continuously

### Process Risks  
- **Methodology Deviation**: Strictly follow established agent persona and project methodology
- **Test Coverage Regression**: Build upon existing test patterns, maintain coverage metrics
- **Project Integration Failure**: Continuously update all 4 active project files

### Business Risks
- **Scope Creep**: Adhere to defined phases, leverage existing project tracking
- **Timeline Risk**: Focus on critical path dependencies, maximize parallel development
- **Quality Risk**: Maintain established standards for testing, documentation, and code quality

## Conclusion

This **completely revised** implementation plan reflects the shocking discovery that ACS is **70% complete** with sophisticated domain models, comprehensive normalizers, and full database schema already implemented. The focus shifts from "building from scratch" to "completing an advanced architecture."

**MAJOR PARADIGM SHIFT:**
- **NOT building domain foundation** - Already exists and is mature
- **NOT creating database schema** - Already complete with seed data
- **NOT establishing patterns** - Operational Normalization Pattern proven with 40+ tests
- **COMPLETING integration gaps** - Database connectivity, API layer, business logic

**Revised Implementation Principles:**
- **Leverage existing excellence** - Build upon proven normalizer patterns and comprehensive domain models
- **Fill critical gaps** - Database integration, API controllers, business logic completion
- **Maintain established quality** - Preserve test coverage, documentation standards, project methodology
- **Accelerated delivery** - 4 focused phases instead of 6 comprehensive phases

**Revolutionary Capabilities Ready for Completion:**
- **Advanced domain architecture** - All models exist with sophisticated relationships
- **Proven normalizer pattern** - Operational complexity management established  
- **Complete database design** - Enterprise-ready schema with audit trails
- **Comprehensive test foundation** - Patterns established for rapid expansion

**Realistic Timeline: 4 weeks instead of 12 weeks**
- **Week 1**: Database integration (connect existing normalizers to EF)
- **Week 2**: API layer creation (controllers and services from scratch)
- **Week 3**: Business logic completion (Authorization class, missing normalizers)
- **Week 4**: Testing, polish, and documentation updates

This discovery transforms ACS from a "prototype to be built" into an "advanced system to be completed." The revolutionary access control capabilities are **already architected** - we're now implementing the final integration layer to make them **production-ready**.