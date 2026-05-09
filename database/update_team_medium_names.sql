-- Populate MediumName and code (TLA) for all Premier League teams
-- Run this against the Supabase database after deploying the DropShortNameAddCode migration
-- Matches on both plain names and football-data.org variants (e.g. "AFC Bournemouth", "FC" suffixes)

UPDATE teams SET "MediumName" = 'Arsenal',        code = 'ARS' WHERE name IN ('Arsenal', 'Arsenal FC');
UPDATE teams SET "MediumName" = 'Aston Villa',    code = 'AVL' WHERE name IN ('Aston Villa', 'Aston Villa FC');
UPDATE teams SET "MediumName" = 'Bournemouth',    code = 'BOU' WHERE name IN ('Bournemouth', 'AFC Bournemouth', 'Bournemouth FC');
UPDATE teams SET "MediumName" = 'Brentford',      code = 'BRE' WHERE name IN ('Brentford', 'Brentford FC');
UPDATE teams SET "MediumName" = 'Brighton',       code = 'BHA' WHERE name IN ('Brighton & Hove Albion', 'Brighton & Hove Albion FC', 'Brighton');
UPDATE teams SET "MediumName" = 'Burnley',        code = 'BUR' WHERE name IN ('Burnley', 'Burnley FC');
UPDATE teams SET "MediumName" = 'Chelsea',        code = 'CHE' WHERE name IN ('Chelsea', 'Chelsea FC');
UPDATE teams SET "MediumName" = 'Crystal Palace', code = 'CRY' WHERE name IN ('Crystal Palace', 'Crystal Palace FC');
UPDATE teams SET "MediumName" = 'Everton',        code = 'EVE' WHERE name IN ('Everton', 'Everton FC');
UPDATE teams SET "MediumName" = 'Fulham',         code = 'FUL' WHERE name IN ('Fulham', 'Fulham FC');
UPDATE teams SET "MediumName" = 'Ipswich',        code = 'IPS' WHERE name IN ('Ipswich Town', 'Ipswich Town FC', 'Ipswich');
UPDATE teams SET "MediumName" = 'Leeds',          code = 'LEE' WHERE name IN ('Leeds United', 'Leeds United FC', 'Leeds');
UPDATE teams SET "MediumName" = 'Leicester',      code = 'LEI' WHERE name IN ('Leicester City', 'Leicester City FC', 'Leicester');
UPDATE teams SET "MediumName" = 'Liverpool',      code = 'LIV' WHERE name IN ('Liverpool', 'Liverpool FC');
UPDATE teams SET "MediumName" = 'Man City',       code = 'MCI' WHERE name IN ('Manchester City', 'Manchester City FC');
UPDATE teams SET "MediumName" = 'Man United',     code = 'MUN' WHERE name IN ('Manchester United', 'Manchester United FC');
UPDATE teams SET "MediumName" = 'Newcastle',      code = 'NEW' WHERE name IN ('Newcastle United', 'Newcastle United FC', 'Newcastle');
UPDATE teams SET "MediumName" = 'Nott''m Forest', code = 'NFO' WHERE name IN ('Nottingham Forest', 'Nottingham Forest FC');
UPDATE teams SET "MediumName" = 'Southampton',    code = 'SOU' WHERE name IN ('Southampton', 'Southampton FC');
UPDATE teams SET "MediumName" = 'Sunderland',     code = 'SUN' WHERE name IN ('Sunderland', 'Sunderland AFC');
UPDATE teams SET "MediumName" = 'Spurs',          code = 'TOT' WHERE name IN ('Tottenham Hotspur', 'Tottenham Hotspur FC', 'Tottenham');
UPDATE teams SET "MediumName" = 'West Ham',       code = 'WHU' WHERE name IN ('West Ham United', 'West Ham United FC', 'West Ham');
UPDATE teams SET "MediumName" = 'Wolves',         code = 'WOL' WHERE name IN ('Wolverhampton Wanderers', 'Wolverhampton Wanderers FC', 'Wolverhampton');
