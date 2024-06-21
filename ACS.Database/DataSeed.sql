-- Insert Sample Data to Exercise the ACS Paradigm
BEGIN TRANSACTION

-- Insert Entities
INSERT INTO Entity (Id) VALUES (1), (2), (3), (4), (5), (6);

-- Insert Groups
INSERT INTO [Group] (Id, Name, EntityId) VALUES (1, 'Development', 5), (2, 'Marketing', 6);

-- Insert Roles
INSERT INTO Role (Id, Name, GroupId, EntityId) VALUES (1, 'Admin', 1, 3), (2, 'User', 2, 4);

-- Insert Users
INSERT INTO [User] (Id, Name, EntityId, RoleId, GroupId) VALUES 
(1, 'Alice', 1, 1, 1), 
(2, 'Bob', 2, 2, 2);

-- Insert VerbTypes
INSERT INTO VerbType (Id, VerbName) VALUES (1, 'GET'), (2, 'POST'), (3, 'PUT'), (4, 'DELETE');

-- Insert Resources
INSERT INTO Resource (Id, Uri) VALUES (1, '/api/data'), (2, '/api/users');

-- Insert SchemeTypes
INSERT INTO SchemeType (Id, SchemeName) VALUES (1, 'API Endpoints Authorization');

-- Insert PermissionSchemes
INSERT INTO PermissionScheme (Id, EntityId, SchemeTypeId) VALUES (1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1), (5, 5, 1), (6, 6, 1);

-- Insert UriAccess (Grant and Deny Permissions)
INSERT INTO UriAccess (Id, ResourceId, VerbTypeId, PermissionSchemeId, [Grant], [Deny]) VALUES 
(1, 1, 1, 1, 1, 0), -- Alice can GET /api/data
(2, 2, 2, 2, 0, 1), -- Bob is denied POST /api/users
(3, 1, 3, 3, 1, 0), -- Admin role can PUT /api/data
(4, 2, 4, 4, 1, 0), -- User role can DELETE /api/users
(5, 1, 1, 5, 1, 0), -- Development group can GET /api/data
(6, 2, 2, 6, 1, 0); -- Marketing group can POST /api/users

-- Insert Audit Logs
INSERT INTO AuditLog (EntityType, EntityId, ChangeType, ChangedBy, ChangeDetails)
VALUES 
('User', 1, 'INSERT', 'system', '{ "Id": 1, "Name": "Alice", "EntityId": 1, "RoleId": 1, "GroupId": 1 }'),
('User', 2, 'INSERT', 'system', '{ "Id": 2, "Name": "Bob", "EntityId": 2, "RoleId": 2, "GroupId": 2 }'),
('Role', 1, 'INSERT', 'system', '{ "Id": 1, "Name": "Admin", "GroupId": 1, "EntityId": 3 }'),
('Role', 2, 'INSERT', 'system', '{ "Id": 2, "Name": "User", "GroupId": 2, "EntityId": 4 }'),
('Group', 1, 'INSERT', 'system', '{ "Id": 1, "Name": "Development", "EntityId": 5 }'),
('Group', 2, 'INSERT', 'system', '{ "Id": 2, "Name": "Marketing", "EntityId": 6 }');

COMMIT TRANSACTION