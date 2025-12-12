-- Row Level Security (RLS) Policies for Premier League Predictions
-- This file implements security policies to protect database tables from unauthorized access
--
-- CRITICAL: Run this script in your Supabase SQL editor immediately
-- WARNING: Without RLS, all tables are publicly accessible via the PostgREST API

-- ============================================================================
-- ENABLE RLS ON ALL TABLES
-- ============================================================================

ALTER TABLE seasons ENABLE ROW LEVEL SECURITY;
ALTER TABLE teams ENABLE ROW LEVEL SECURITY;
ALTER TABLE gameweeks ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixtures ENABLE ROW LEVEL SECURITY;
ALTER TABLE users ENABLE ROW LEVEL SECURITY;
ALTER TABLE picks ENABLE ROW LEVEL SECURITY;
ALTER TABLE team_selections ENABLE ROW LEVEL SECURITY;
ALTER TABLE email_notifications ENABLE ROW LEVEL SECURITY;
ALTER TABLE admin_actions ENABLE ROW LEVEL SECURITY;

-- Enable RLS on tables that may exist but weren't in schema.sql
ALTER TABLE IF EXISTS season_participations ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS user_eliminations ENABLE ROW LEVEL SECURITY;
ALTER TABLE IF EXISTS pick_rules ENABLE ROW LEVEL SECURITY;

-- ============================================================================
-- HELPER FUNCTIONS
-- ============================================================================

-- Function to check if the current user is an admin
CREATE OR REPLACE FUNCTION is_admin()
RETURNS BOOLEAN AS $$
BEGIN
  RETURN (
    SELECT is_admin
    FROM users
    WHERE id = auth.uid()
  );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Function to get the current user's UUID
CREATE OR REPLACE FUNCTION current_user_id()
RETURNS UUID AS $$
BEGIN
  RETURN auth.uid();
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- ============================================================================
-- SEASONS TABLE - Public Read, Admin Write
-- ============================================================================

-- Allow everyone to read seasons
CREATE POLICY "Anyone can view seasons"
  ON seasons FOR SELECT
  USING (true);

-- Only admins can insert seasons
CREATE POLICY "Only admins can insert seasons"
  ON seasons FOR INSERT
  WITH CHECK (is_admin());

-- Only admins can update seasons
CREATE POLICY "Only admins can update seasons"
  ON seasons FOR UPDATE
  USING (is_admin())
  WITH CHECK (is_admin());

-- Only admins can delete seasons
CREATE POLICY "Only admins can delete seasons"
  ON seasons FOR DELETE
  USING (is_admin());

-- ============================================================================
-- TEAMS TABLE - Public Read, Admin Write
-- ============================================================================

-- Allow everyone to read teams
CREATE POLICY "Anyone can view teams"
  ON teams FOR SELECT
  USING (true);

-- Only admins can insert teams
CREATE POLICY "Only admins can insert teams"
  ON teams FOR INSERT
  WITH CHECK (is_admin());

-- Only admins can update teams
CREATE POLICY "Only admins can update teams"
  ON teams FOR UPDATE
  USING (is_admin())
  WITH CHECK (is_admin());

-- Only admins can delete teams
CREATE POLICY "Only admins can delete teams"
  ON teams FOR DELETE
  USING (is_admin());

-- ============================================================================
-- GAMEWEEKS TABLE - Public Read, Admin Write
-- ============================================================================

-- Allow everyone to read gameweeks
CREATE POLICY "Anyone can view gameweeks"
  ON gameweeks FOR SELECT
  USING (true);

-- Only admins can insert gameweeks
CREATE POLICY "Only admins can insert gameweeks"
  ON gameweeks FOR INSERT
  WITH CHECK (is_admin());

-- Only admins can update gameweeks
CREATE POLICY "Only admins can update gameweeks"
  ON gameweeks FOR UPDATE
  USING (is_admin())
  WITH CHECK (is_admin());

-- Only admins can delete gameweeks
CREATE POLICY "Only admins can delete gameweeks"
  ON gameweeks FOR DELETE
  USING (is_admin());

-- ============================================================================
-- FIXTURES TABLE - Public Read, Admin Write
-- ============================================================================

-- Allow everyone to read fixtures
CREATE POLICY "Anyone can view fixtures"
  ON fixtures FOR SELECT
  USING (true);

-- Only admins can insert fixtures
CREATE POLICY "Only admins can insert fixtures"
  ON fixtures FOR INSERT
  WITH CHECK (is_admin());

-- Only admins can update fixtures
CREATE POLICY "Only admins can update fixtures"
  ON fixtures FOR UPDATE
  USING (is_admin())
  WITH CHECK (is_admin());

-- Only admins can delete fixtures
CREATE POLICY "Only admins can delete fixtures"
  ON fixtures FOR DELETE
  USING (is_admin());

-- ============================================================================
-- USERS TABLE - Users Read Own, Admins Read All, Limited Self-Update
-- ============================================================================

-- Users can view their own profile, admins can view all
CREATE POLICY "Users can view their own profile, admins view all"
  ON users FOR SELECT
  USING (
    auth.uid() = id OR is_admin()
  );

-- Only admins can insert users (handled via auth triggers typically)
CREATE POLICY "Only admins can insert users"
  ON users FOR INSERT
  WITH CHECK (is_admin());

-- Users can update their own limited fields, admins can update all
CREATE POLICY "Users can update own profile, admins can update all"
  ON users FOR UPDATE
  USING (auth.uid() = id OR is_admin())
  WITH CHECK (
    (auth.uid() = id AND is_admin() = false) OR -- User updating self (can't elevate admin)
    is_admin() -- Admin can update anything
  );

-- Only admins can delete users
CREATE POLICY "Only admins can delete users"
  ON users FOR DELETE
  USING (is_admin());

-- ============================================================================
-- PICKS TABLE - Users Read All, Create/Update Own, Admin Full Access
-- ============================================================================

-- Anyone authenticated can view all picks (for leaderboards)
CREATE POLICY "Authenticated users can view all picks"
  ON picks FOR SELECT
  USING (auth.uid() IS NOT NULL);

-- Users can only insert their own picks
CREATE POLICY "Users can insert their own picks"
  ON picks FOR INSERT
  WITH CHECK (auth.uid() = user_id OR is_admin());

-- Users can only update their own picks (before gameweek locks)
CREATE POLICY "Users can update their own picks"
  ON picks FOR UPDATE
  USING (auth.uid() = user_id OR is_admin())
  WITH CHECK (auth.uid() = user_id OR is_admin());

-- Only admins can delete picks
CREATE POLICY "Only admins can delete picks"
  ON picks FOR DELETE
  USING (is_admin());

-- ============================================================================
-- TEAM_SELECTIONS TABLE - Users Read All, Create/Update Own
-- ============================================================================

-- Anyone authenticated can view all team selections (to see what teams are available)
CREATE POLICY "Authenticated users can view all team selections"
  ON team_selections FOR SELECT
  USING (auth.uid() IS NOT NULL);

-- Users can only insert their own team selections
CREATE POLICY "Users can insert their own team selections"
  ON team_selections FOR INSERT
  WITH CHECK (auth.uid() = user_id OR is_admin());

-- Users can only update their own team selections
CREATE POLICY "Users can update their own team selections"
  ON team_selections FOR UPDATE
  USING (auth.uid() = user_id OR is_admin())
  WITH CHECK (auth.uid() = user_id OR is_admin());

-- Only admins can delete team selections
CREATE POLICY "Only admins can delete team selections"
  ON team_selections FOR DELETE
  USING (is_admin());

-- ============================================================================
-- EMAIL_NOTIFICATIONS TABLE - Admin Only
-- ============================================================================

-- Only admins can view email notifications
CREATE POLICY "Only admins can view email notifications"
  ON email_notifications FOR SELECT
  USING (is_admin());

-- Only admins can insert email notifications
CREATE POLICY "Only admins can insert email notifications"
  ON email_notifications FOR INSERT
  WITH CHECK (is_admin());

-- Only admins can update email notifications
CREATE POLICY "Only admins can update email notifications"
  ON email_notifications FOR UPDATE
  USING (is_admin())
  WITH CHECK (is_admin());

-- Only admins can delete email notifications
CREATE POLICY "Only admins can delete email notifications"
  ON email_notifications FOR DELETE
  USING (is_admin());

-- ============================================================================
-- ADMIN_ACTIONS TABLE - Admin Only
-- ============================================================================

-- Only admins can view admin actions
CREATE POLICY "Only admins can view admin actions"
  ON admin_actions FOR SELECT
  USING (is_admin());

-- Only admins can insert admin actions
CREATE POLICY "Only admins can insert admin actions"
  ON admin_actions FOR INSERT
  WITH CHECK (is_admin());

-- Only admins can update admin actions
CREATE POLICY "Only admins can update admin actions"
  ON admin_actions FOR UPDATE
  USING (is_admin())
  WITH CHECK (is_admin());

-- Only admins can delete admin actions
CREATE POLICY "Only admins can delete admin actions"
  ON admin_actions FOR DELETE
  USING (is_admin());

-- ============================================================================
-- SEASON_PARTICIPATIONS TABLE (if exists) - Users Read All, Create/Update Own
-- ============================================================================

-- Anyone authenticated can view all season participations
CREATE POLICY "Authenticated users can view all season participations"
  ON season_participations FOR SELECT
  USING (auth.uid() IS NOT NULL);

-- Users can insert their own season participations
CREATE POLICY "Users can insert their own season participations"
  ON season_participations FOR INSERT
  WITH CHECK (auth.uid() = user_id OR is_admin());

-- Users can update their own season participations
CREATE POLICY "Users can update their own season participations"
  ON season_participations FOR UPDATE
  USING (auth.uid() = user_id OR is_admin())
  WITH CHECK (auth.uid() = user_id OR is_admin());

-- Only admins can delete season participations
CREATE POLICY "Only admins can delete season participations"
  ON season_participations FOR DELETE
  USING (is_admin());

-- ============================================================================
-- USER_ELIMINATIONS TABLE (if exists) - Read All, Admin Write
-- ============================================================================

-- Anyone authenticated can view all user eliminations (public leaderboard data)
CREATE POLICY "Authenticated users can view all user eliminations"
  ON user_eliminations FOR SELECT
  USING (auth.uid() IS NOT NULL);

-- Only admins can insert user eliminations
CREATE POLICY "Only admins can insert user eliminations"
  ON user_eliminations FOR INSERT
  WITH CHECK (is_admin());

-- Only admins can update user eliminations
CREATE POLICY "Only admins can update user eliminations"
  ON user_eliminations FOR UPDATE
  USING (is_admin())
  WITH CHECK (is_admin());

-- Only admins can delete user eliminations
CREATE POLICY "Only admins can delete user eliminations"
  ON user_eliminations FOR DELETE
  USING (is_admin());

-- ============================================================================
-- PICK_RULES TABLE (if exists) - Public Read, Admin Write
-- ============================================================================

-- Anyone can view pick rules
CREATE POLICY "Anyone can view pick rules"
  ON pick_rules FOR SELECT
  USING (true);

-- Only admins can insert pick rules
CREATE POLICY "Only admins can insert pick rules"
  ON pick_rules FOR INSERT
  WITH CHECK (is_admin());

-- Only admins can update pick rules
CREATE POLICY "Only admins can update pick rules"
  ON pick_rules FOR UPDATE
  USING (is_admin())
  WITH CHECK (is_admin());

-- Only admins can delete pick rules
CREATE POLICY "Only admins can delete pick rules"
  ON pick_rules FOR DELETE
  USING (is_admin());

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

-- Run these queries after applying the policies to verify RLS is enabled
-- SELECT tablename, rowsecurity FROM pg_tables WHERE schemaname = 'public';
-- SELECT * FROM pg_policies WHERE schemaname = 'public';
