# Backend Integration with Row Level Security

## Architecture Overview

Your application uses a **C# backend with Entity Framework** connecting directly to PostgreSQL, rather than using Supabase client libraries. This is a common and valid architecture.

## Important: RLS Bypass Behavior

⚠️ **Your backend currently BYPASSES RLS policies** because:

1. The backend uses a direct PostgreSQL connection (via Entity Framework)
2. The connection string uses the database owner credentials
3. RLS policies do NOT apply to the table owner or superuser roles

This is actually **GOOD** for your architecture because:
- ✅ Your backend acts as the security layer
- ✅ Your API endpoints control access
- ✅ No need to manage JWT context in backend queries
- ✅ Simpler database operations without RLS overhead

## Security Architecture

```
┌─────────────┐
│   Frontend  │
│  (React)    │
└──────┬──────┘
       │ HTTP Requests
       │ (JWT Auth Header)
       ▼
┌─────────────────────┐
│   C# Backend API    │ ◄─── YOUR SECURITY LAYER
│   (Authorization)   │      (Validates JWT, checks permissions)
└──────┬──────────────┘
       │ Direct SQL via EF
       │ (Full database access)
       ▼
┌─────────────────────┐
│   PostgreSQL DB     │
│   (RLS Enabled)     │ ◄─── RLS protects against direct PostgREST access
└─────────────────────┘
```

## When RLS is Applied

RLS policies will ONLY be enforced for:
- ✅ Direct Supabase PostgREST API calls (if you enable PostgREST)
- ✅ Supabase client library calls from frontend (if using Supabase JS client)
- ✅ Direct database connections using non-owner roles

RLS policies will NOT be enforced for:
- ❌ Your C# backend's Entity Framework queries
- ❌ Any connection using the database owner role
- ❌ Superuser connections

## Current Security Implementation

### Backend Authorization Pattern

Your backend should already implement authorization like this:

```csharp
[Authorize] // Validates JWT
[HttpGet("picks/my")]
public async Task<IActionResult> GetMyPicks()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // Backend enforces the "users can only see their own picks" rule
    var picks = await _context.Picks
        .Where(p => p.UserId == userId)
        .ToListAsync();

    return Ok(picks);
}
```

This is equivalent to RLS, but enforced at the API layer instead of the database layer.

## Do You Need RLS?

### You NEED RLS if:
- ☑️ You expose Supabase PostgREST API to frontend
- ☑️ You use Supabase client libraries in frontend
- ☑️ You allow direct database access from frontend

### You DON'T NEED RLS if:
- ☑️ All database access goes through your C# backend
- ☑️ Your backend validates all requests
- ☑️ Frontend never directly queries the database

## Recommended Approach

Based on your architecture, I recommend **HYBRID SECURITY**:

### 1. Enable RLS (Defense in Depth)

Even though your backend bypasses it, enable RLS as a safety net:
- Protects against accidental PostgREST exposure
- Protects against misconfigured Supabase settings
- Follows security best practices (defense in depth)
- No performance impact on your backend queries

✅ **Apply the RLS policies from `enable_rls.sql`**

### 2. Backend Remains Primary Security Layer

Keep your existing backend authorization:
- Continue using `[Authorize]` attributes
- Continue validating user permissions in controllers
- Continue filtering queries by user ID

### 3. Disable PostgREST (Recommended)

If you're not using Supabase PostgREST API:

```sql
-- In Supabase Dashboard > API Settings
-- Disable API auto-generation for your schema
```

Or explicitly in SQL:
```sql
-- Revoke public schema access from PostgREST role
REVOKE USAGE ON SCHEMA public FROM anon;
REVOKE USAGE ON SCHEMA public FROM authenticated;
```

## Testing Your Setup

### Test 1: Backend Should Still Work
Your C# backend should continue working normally after applying RLS:

```bash
# These should all work as before
curl -H "Authorization: Bearer YOUR_JWT" https://your-api.com/api/picks/my
curl -H "Authorization: Bearer YOUR_JWT" https://your-api.com/api/users/profile
```

### Test 2: Direct Database Access Should Be Blocked

If someone tries to access the database directly via PostgREST (without going through your backend):

```bash
# This should fail or return empty results
curl https://your-supabase-url.supabase.co/rest/v1/users
```

## Environment Variables Check

Ensure your production environment has:

```bash
# Backend connection (full access, bypasses RLS)
DATABASE_CONNECTION_STRING="Host=db.xxx.supabase.co;Database=postgres;Username=postgres;Password=xxx"

# If using Supabase features directly
SUPABASE_URL="https://xxx.supabase.co"
SUPABASE_SERVICE_ROLE_KEY="eyJ..." # Keep secret! Never expose to frontend
SUPABASE_ANON_KEY="eyJ..." # Public key, limited access
```

## Migration Checklist

- [ ] Apply RLS policies from `enable_rls.sql`
- [ ] Verify backend still works (test all endpoints)
- [ ] Confirm RLS is enabled: `SELECT tablename, rowsecurity FROM pg_tables WHERE schemaname = 'public';`
- [ ] Test that direct PostgREST access is blocked (if applicable)
- [ ] Review backend authorization for all endpoints
- [ ] Document any endpoints that need admin access
- [ ] Consider disabling PostgREST if not used

## Common Backend Patterns

### Admin-Only Endpoints
```csharp
[Authorize]
[HttpPost("admin/users/{id}/make-admin")]
public async Task<IActionResult> MakeUserAdmin(Guid id)
{
    // Backend checks if current user is admin
    var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var currentUser = await _context.Users.FindAsync(Guid.Parse(currentUserId));

    if (!currentUser.IsAdmin)
        return Forbid();

    var targetUser = await _context.Users.FindAsync(id);
    targetUser.IsAdmin = true;
    await _context.SaveChangesAsync();

    return Ok();
}
```

### User-Scoped Queries
```csharp
[Authorize]
[HttpGet("picks")]
public async Task<IActionResult> GetPicks([FromQuery] int gameweekId)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // Only return the current user's pick for this gameweek
    var pick = await _context.Picks
        .Include(p => p.Team)
        .FirstOrDefaultAsync(p =>
            p.UserId == Guid.Parse(userId) &&
            p.GameweekId == gameweekId);

    return Ok(pick);
}
```

## Performance Considerations

Since your backend bypasses RLS:
- ✅ No performance impact from RLS policies
- ✅ No need to set session variables
- ✅ Standard Entity Framework performance
- ✅ Can use EF query optimization normally

## Security Audit Questions

Ask yourself:
1. ✅ Do all endpoints have `[Authorize]` attribute?
2. ✅ Do user-scoped endpoints filter by `User.FindFirst(ClaimTypes.NameIdentifier)`?
3. ✅ Do admin endpoints check `IsAdmin` flag?
4. ✅ Are you validating user permissions before data access?
5. ✅ Are you using parameterized queries (EF does this automatically)?

If yes to all, your security is solid even if backend bypasses RLS.

## Summary

| Security Layer | Purpose | Status |
|----------------|---------|--------|
| **RLS Policies** | Protect against direct PostgREST access | ✅ Apply as defense in depth |
| **Backend Authorization** | Primary security enforcement | ✅ Already implemented |
| **JWT Validation** | Authenticate users | ✅ Already implemented |
| **API Rate Limiting** | Prevent abuse | ✅ Already configured |

Your architecture is secure. Apply RLS as an additional safety layer, but your C# backend remains the primary security boundary.
