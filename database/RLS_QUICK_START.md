# 🚨 RLS Security Issue - Quick Action Guide

## What's the Problem?

Supabase detected **12 database tables without Row Level Security (RLS)** enabled. Without RLS, these tables could be accessed directly via PostgREST API by anyone with your Supabase URL.

## Impact Assessment

### Your Current Architecture: ✅ MOSTLY SAFE

Your app uses:
- **C# Backend with Entity Framework** → Bypasses RLS (this is normal)
- **Backend API endpoints** → Your primary security layer
- **JWT Authentication** → Already implemented

### Where You're Vulnerable: ⚠️

If Supabase PostgREST API is enabled (default), anyone could potentially:
- Read user data directly from `https://your-project.supabase.co/rest/v1/users`
- Modify picks via direct API calls
- Access admin data

## Immediate Actions Required

### Step 1: Apply RLS Policies (5 minutes)

1. Open [Supabase Dashboard](https://app.supabase.com) → SQL Editor
2. Copy and paste the entire contents of `database/enable_rls.sql`
3. Click "Run"

This enables RLS on all 12 tables and creates secure access policies.

### Step 2: Verify RLS is Active (1 minute)

In Supabase SQL Editor, run:

```sql
SELECT tablename, rowsecurity
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY tablename;
```

✅ All tables should show `rowsecurity = t` (true)

### Step 3: Test Your Backend (5 minutes)

Ensure your backend still works:

```bash
# Test authentication
curl https://your-api.com/api/auth/login

# Test user endpoints
curl -H "Authorization: Bearer YOUR_JWT" https://your-api.com/api/picks/my

# Test admin endpoints
curl -H "Authorization: Bearer ADMIN_JWT" https://your-api.com/api/admin/users
```

All should work exactly as before.

### Step 4: Disable PostgREST (Optional but Recommended)

If you don't use Supabase PostgREST API directly from your frontend:

In Supabase SQL Editor:
```sql
-- Revoke public access to PostgREST
REVOKE USAGE ON SCHEMA public FROM anon;
REVOKE USAGE ON SCHEMA public FROM authenticated;
```

## Understanding Your Security Layers

```
Internet
   ↓
┌────────────────────────┐
│   Frontend (React)     │
└───────────┬────────────┘
            │ HTTP + JWT
            ↓
┌────────────────────────┐
│   C# Backend API       │ ← PRIMARY SECURITY LAYER
│   • JWT validation     │   (Your backend does the heavy lifting)
│   • Authorization      │
│   • Business logic     │
└───────────┬────────────┘
            │ Direct SQL (bypasses RLS)
            ↓
┌────────────────────────┐
│   PostgreSQL + RLS     │ ← BACKUP SECURITY LAYER
│   (Blocks direct       │   (Protects against PostgREST access)
│    PostgREST access)   │
└────────────────────────┘
```

## Why Your Backend Bypasses RLS

This is **NORMAL and EXPECTED** because:
- Your backend uses the database owner credentials
- PostgreSQL doesn't apply RLS to the table owner
- Your C# API handles all security checks

RLS protects against:
- ✅ Direct PostgREST API calls bypassing your backend
- ✅ Accidental exposure of database API
- ✅ Misconfigured Supabase settings

RLS does NOT protect against:
- ❌ Vulnerabilities in your C# backend code
- ❌ Compromised backend credentials
- ❌ SQL injection (use parameterized queries - EF does this automatically)

## Files Created for You

| File | Purpose |
|------|---------|
| `enable_rls.sql` | Complete RLS policies for all 12 tables |
| `RLS_IMPLEMENTATION.md` | Detailed implementation guide |
| `BACKEND_RLS_INTEGRATION.md` | How RLS works with your C# backend |
| `RLS_QUICK_START.md` | This file - quick action guide |

## Affected Tables

| Table | Access Pattern |
|-------|----------------|
| `seasons`, `teams`, `gameweeks`, `fixtures` | Public read, admin write |
| `users` | Users read own, admin read all |
| `picks`, `team_selections` | Users manage own, all can view |
| `admin_actions`, `email_notifications` | Admin only |
| `season_participations`, `user_eliminations` | Mixed access |
| `pick_rules` | Public read, admin write |

## Testing the Fix

### Before RLS (Vulnerable):
```bash
# Anyone can read users
curl https://xxx.supabase.co/rest/v1/users \
  -H "apikey: YOUR_ANON_KEY"

# Returns: All user data! 🚨
```

### After RLS (Protected):
```bash
# Same request
curl https://xxx.supabase.co/rest/v1/users \
  -H "apikey: YOUR_ANON_KEY"

# Returns: Empty or only authenticated user's own data ✅
```

### Your Backend (Always Works):
```bash
# Via your C# API
curl https://your-api.com/api/users/profile \
  -H "Authorization: Bearer YOUR_JWT"

# Returns: User's profile (backend enforces security) ✅
```

## FAQ

**Q: Will this break my backend?**
A: No. Your backend bypasses RLS and will work exactly as before.

**Q: Will this slow down queries?**
A: No impact on your backend. RLS only adds overhead to PostgREST requests.

**Q: Do I need to change backend code?**
A: No. Your existing authorization logic is fine. RLS is an additional layer.

**Q: What if I don't use PostgREST?**
A: Still apply RLS as defense-in-depth. It's free insurance.

**Q: Can I skip this?**
A: Not recommended. It takes 5 minutes and protects against accidental exposure.

## Priority Level: 🔴 HIGH

- **Severity**: High (data exposure risk)
- **Effort**: Low (5 minutes to fix)
- **Impact**: High (closes security hole)
- **Breaking Changes**: None

## Next Steps After Applying RLS

1. ✅ Monitor Supabase logs for policy violations
2. ✅ Run security audit on backend endpoints
3. ✅ Review admin access controls
4. ✅ Update deployment documentation
5. ✅ Consider penetration testing

## Support

- 📚 [Supabase RLS Docs](https://supabase.com/docs/guides/auth/row-level-security)
- 📚 [PostgreSQL RLS Docs](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- 📁 See `RLS_IMPLEMENTATION.md` for detailed guide
- 📁 See `BACKEND_RLS_INTEGRATION.md` for C# backend specifics

## Checklist

- [ ] Paste `enable_rls.sql` into Supabase SQL Editor
- [ ] Run the SQL script
- [ ] Verify all tables have RLS enabled
- [ ] Test backend endpoints still work
- [ ] Test direct PostgREST access is blocked
- [ ] Consider disabling PostgREST if not used
- [ ] Document the security architecture
- [ ] Commit these files to version control

---

**Time to fix: 5 minutes**
**Priority: HIGH**
**Breaking changes: None**

Run the SQL script now, then read the detailed guides if you want to understand more.
