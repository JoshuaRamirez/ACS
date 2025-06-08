# Database Design

The SQL project defines the tables required by the service. Each table is stored under the `Tables` folder and can be deployed using Visual Studio or `sqlpackage`.

## Schema
Entities include `User`, `Role`, `PermissionScheme`, and supporting tables.

## Relationships
Foreign keys link users to groups and roles, allowing fine‑grained permission management.

## Indexes
Appropriate indexes should be created for lookup columns such as user names and resource identifiers.
