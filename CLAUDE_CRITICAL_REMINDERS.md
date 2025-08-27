# CRITICAL REMINDERS FOR CLAUDE CODE OPERATIONS

## ⚠️ NEVER DELETE FILES UNLESS EXPLICITLY REQUESTED

### What I Did Wrong:
- **DELETED entire controller files** during clean architecture implementation
- **Lost git history** and caused confusion
- **Should have modified files in place** instead of deleting and recreating

### STRICT RULES TO FOLLOW:

1. **🚫 NEVER DELETE FILES** unless the user explicitly asks for deletion
2. **✏️ ALWAYS MODIFY IN PLACE** - use Edit/MultiEdit tools to change existing files
3. **🔄 PRESERVE GIT HISTORY** - modifications show as "modified", not "deleted"
4. **📝 ASK BEFORE MAJOR CHANGES** - if unsure about file operations, ask the user first
5. **🔍 READ BEFORE EDITING** - always use Read tool to understand file content before changing
6. **🧪 TEST INCREMENTALLY** - make small changes and test builds frequently

### CORRECT APPROACH FOR REFACTORING:
```
❌ WRONG: rm file.cs && create new file.cs
✅ RIGHT: Edit file.cs to update dependencies and logic
```

### ACCEPTABLE FILE OPERATIONS:
- ✅ **Edit** - Modify existing files
- ✅ **MultiEdit** - Make multiple changes to existing files  
- ✅ **Write** - Create NEW files when explicitly needed
- ✅ **Read** - Always read before modifying

### UNACCEPTABLE OPERATIONS:
- ❌ **Delete files** without explicit user request
- ❌ **Remove files** to "simplify" refactoring
- ❌ **Recreate files** instead of modifying them

## REMEMBER: 
**Modification preserves history. Deletion destroys history.**
**When in doubt, modify in place, don't delete and recreate.**

---
*This reminder exists because I accidentally deleted controller files during clean architecture implementation on 2025-01-27. Never repeat this mistake.*