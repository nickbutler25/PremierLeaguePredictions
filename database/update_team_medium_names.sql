-- Populate MediumName and ShortName for all Premier League teams
-- Run this against the Supabase database after deploying the AddTeamMediumName migration
-- Matches on both plain names and football-data.org variants (e.g. "AFC Bournemouth", "FC" suffixes)

UPDATE teams SET "MediumName" = 'Arsenal',        "ShortName" = 'ARS' WHERE name IN ('Arsenal', 'Arsenal FC');
UPDATE teams SET "MediumName" = 'Aston Villa',    "ShortName" = 'AVL' WHERE name IN ('Aston Villa', 'Aston Villa FC');
UPDATE teams SET "MediumName" = 'Bournemouth',    "ShortName" = 'BOU' WHERE name IN ('Bournemouth', 'AFC Bournemouth', 'Bournemouth FC');
UPDATE teams SET "MediumName" = 'Brentford',      "ShortName" = 'BRE' WHERE name IN ('Brentford', 'Brentford FC');
UPDATE teams SET "MediumName" = 'Brighton',       "ShortName" = 'BHA' WHERE name IN ('Brighton & Hove Albion', 'Brighton & Hove Albion FC', 'Brighton');
UPDATE teams SET "MediumName" = 'Burnley',        "ShortName" = 'BUR' WHERE name IN ('Burnley', 'Burnley FC');
UPDATE teams SET "MediumName" = 'Chelsea',        "ShortName" = 'CHE' WHERE name IN ('Chelsea', 'Chelsea FC');
UPDATE teams SET "MediumName" = 'Crystal Palace', "ShortName" = 'CRY' WHERE name IN ('Crystal Palace', 'Crystal Palace FC');
UPDATE teams SET "MediumName" = 'Everton',        "ShortName" = 'EVE' WHERE name IN ('Everton', 'Everton FC');
UPDATE teams SET "MediumName" = 'Fulham',         "ShortName" = 'FUL' WHERE name IN ('Fulham', 'Fulham FC');
UPDATE teams SET "MediumName" = 'Ipswich',        "ShortName" = 'IPS' WHERE name IN ('Ipswich Town', 'Ipswich Town FC', 'Ipswich');
UPDATE teams SET "MediumName" = 'Leeds',          "ShortName" = 'LEE' WHERE name IN ('Leeds United', 'Leeds United FC', 'Leeds');
UPDATE teams SET "MediumName" = 'Leicester',      "ShortName" = 'LEI' WHERE name IN ('Leicester City', 'Leicester City FC', 'Leicester');
UPDATE teams SET "MediumName" = 'Liverpool',      "ShortName" = 'LIV' WHERE name IN ('Liverpool', 'Liverpool FC');
UPDATE teams SET "MediumName" = 'Man City',       "ShortName" = 'MCI' WHERE name IN ('Manchester City', 'Manchester City FC');
UPDATE teams SET "MediumName" = 'Man United',     "ShortName" = 'MUN' WHERE name IN ('Manchester United', 'Manchester United FC');
UPDATE teams SET "MediumName" = 'Newcastle',      "ShortName" = 'NEW' WHERE name IN ('Newcastle United', 'Newcastle United FC', 'Newcastle');
UPDATE teams SET "MediumName" = 'Nott''m Forest', "ShortName" = 'NFO' WHERE name IN ('Nottingham Forest', 'Nottingham Forest FC');
UPDATE teams SET "MediumName" = 'Southampton',    "ShortName" = 'SOU' WHERE name IN ('Southampton', 'Southampton FC');
UPDATE teams SET "MediumName" = 'Sunderland',     "ShortName" = 'SUN' WHERE name IN ('Sunderland', 'Sunderland AFC');
UPDATE teams SET "MediumName" = 'Spurs',          "ShortName" = 'TOT' WHERE name IN ('Tottenham Hotspur', 'Tottenham Hotspur FC', 'Tottenham');
UPDATE teams SET "MediumName" = 'West Ham',       "ShortName" = 'WHU' WHERE name IN ('West Ham United', 'West Ham United FC', 'West Ham');
UPDATE teams SET "MediumName" = 'Wolves',         "ShortName" = 'WOL' WHERE name IN ('Wolverhampton Wanderers', 'Wolverhampton Wanderers FC', 'Wolverhampton');
