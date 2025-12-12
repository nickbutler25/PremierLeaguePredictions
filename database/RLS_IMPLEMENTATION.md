# Row Level Security (RLS) Implementation Guide

## 🚨 CRITICAL SECURITY ISSUE

Your database has **12 tables exposed without Row Level Security**, meaning anyone with your Supabase API URL can potentially read, modify, or delete data without authentication.

## Impact

Without RLS enabled:
- ❌ Anyone can read all user data (emails, phone numbers, admin status)
- ❌ Anyone can modify picks, team selections, or user profiles
- ❌ Anyone can view admin actions and email logs
- ❌ Anyone can delete records from any table

## Immediate Action Required

### Step 1: Apply RLS Policies

1. Open your Supabase Dashboard
2. Navigate to **SQL Editor**
3. Copy the entire contents of `database/enable_rls.sql`
4. Paste and execute in the SQL Editor

This will:
- ✅ Enable RLS on all tables
- ✅ Create secure access policies
- ✅ Protect sensitive data

### Step 2: Verify RLS is Enabled

Run this query in the SQL Editor:

```sql
SELECT tablename, rowsecurity
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY tablename;
```

All tables should show `rowsecurity = true`.

### Step 3: Test Your Application

After applying RLS, test these scenarios:

1. **As a regular user:**
   - ✅ Can view seasons, teams, gameweeks, fixtures
   - ✅ Can create/update own picks
   - ✅ Can view other users' picks (for leaderboard)
   - ❌ Cannot modify other users' data
   - ❌ Cannot access admin tables

2. **As an admin:**
   - ✅ Full access to all tables
   - ✅ Can manage users, seasons, fixtures
   - ✅ Can view admin actions and email logs

3. **As an unauthenticated user:**
   - ✅ Can view public data (seasons, teams, fixtures, gameweeks)
   - ❌ Cannot access user-specific data
   - ❌ Cannot create or modify any data

## RLS Policy Summary

### 📖 Public Read Tables
Anyone (including unauthenticated users) can read, only admins can write:
- `seasons` - Competition seasons
- `teams` - Premier League teams
- `gameweeks` - Weekly game periods
- `fixtures` - Match fixtures
- `pick_rules` - Game rules (if exists)

### 👤 User-Specific Tables
Users can manage their own data, admins have full access:
- `users` - User profiles (read own, admins read all)
- `picks` - Match predictions (users create/update own, all can read)
- `team_selections` - Team usage tracking (users manage own, all can read)
- `season_participations` - Season enrollment (users manage own, all can read)

### 🔒 Admin-Only Tables
Only admins can access:
- `admin_actions` - Audit log of admin operations
- `email_notifications` - Email delivery tracking
- `user_eliminations` - Elimination tracking (read all, admin write)

## Authentication Integration

The RLS policies rely on Supabase Auth. Ensure your application:

1. **Uses Supabase Auth** for user authentication
2. **Sets auth.uid()** automatically via Supabase client
3. **Maintains is_admin flag** in the users table

### Backend Requirements

If you're using a custom backend (like the C# backend in this project), you need to:

1. **Use Supabase Service Role Key** for admin operations
2. **Use User JWT tokens** for user-scoped operations
3. **Never expose the service role key** to the frontend

Example in C#:
```csharp
// For user-scoped operations
var client = new SupabaseClient(url, anonKey);
await client.Auth.SetSession(userJwtToken);

// For admin operations
var adminClient = new SupabaseClient(url, serviceRoleKey);
```

## Testing RLS Policies

### Test as Unauthenticated User
```sql
-- Should return data
SELECT * FROM seasons;
SELECT * FROM teams;

-- Should return empty (no access)
SELECT * FROM users;
SELECT * FROM picks;
```

### Test as Regular User
```sql
-- Set the context (replace with actual user UUID)
SELECT set_config('request.jwt.claims', '{"sub": "user-uuid-here"}', true);

-- Should return only your data
SELECT * FROM users WHERE id = auth.uid();

-- Should return all picks (for leaderboard)
SELECT * FROM picks;

-- Should fail (admin only)
SELECT * FROM admin_actions;
```

## Common Issues

### Issue: "new row violates row-level security policy"
**Cause:** Trying to insert/update data that violates RLS policies
**Solution:** Ensure the user is authenticated and has permission for that operation

### Issue: Users can't see their own data
**Cause:** User is not properly authenticated or auth.uid() is null
**Solution:** Verify Supabase client is initialized with user's JWT token

### Issue: Admin operations failing
**Cause:** User's `is_admin` flag is false or not set
**Solution:** Update the user record: `UPDATE users SET is_admin = true WHERE id = 'user-uuid';`

## Rollback Plan

If you need to temporarily disable RLS (NOT RECOMMENDED for production):

```sql
-- Disable RLS on a specific table
ALTER TABLE table_name DISABLE ROW LEVEL SECURITY;

-- Re-enable when ready
ALTER TABLE table_name ENABLE ROW LEVEL SECURITY;
```

## Next Steps

1. ✅ Apply the RLS policies immediately
2. ✅ Test your application thoroughly
3. ✅ Update your backend to use appropriate credentials
4. ✅ Monitor Supabase logs for RLS policy violations
5. ✅ Document any custom RLS policies for future reference

## Additional Resources

- [Supabase RLS Documentation](https://supabase.com/docs/guides/auth/row-level-security)
- [PostgreSQL RLS Documentation](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- [Supabase Security Best Practices](https://supabase.com/docs/guides/platform/going-into-prod)
