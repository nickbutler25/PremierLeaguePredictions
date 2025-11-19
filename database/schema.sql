-- Premier League Predictions Database Schema

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(255) UNIQUE NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone_number VARCHAR(20),
    photo_url VARCHAR(500),
    google_id VARCHAR(255) UNIQUE,
    is_active BOOLEAN DEFAULT true,
    is_admin BOOLEAN DEFAULT false,
    is_paid BOOLEAN DEFAULT false,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Seasons table
CREATE TABLE seasons (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    is_active BOOLEAN DEFAULT false,
    is_archived BOOLEAN DEFAULT false,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Teams table
CREATE TABLE teams (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) NOT NULL,
    short_name VARCHAR(50),
    code VARCHAR(10),
    logo_url VARCHAR(500),
    external_api_id INTEGER UNIQUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Gameweeks table
CREATE TABLE gameweeks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    season_id UUID REFERENCES seasons(id) ON DELETE CASCADE,
    week_number INTEGER NOT NULL,
    deadline TIMESTAMP WITH TIME ZONE NOT NULL,
    is_locked BOOLEAN DEFAULT false,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_season_week UNIQUE (season_id, week_number),
    CONSTRAINT valid_week_number CHECK (week_number >= 1 AND week_number <= 38)
);

-- Fixtures table
CREATE TABLE fixtures (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    gameweek_id UUID REFERENCES gameweeks(id) ON DELETE CASCADE,
    home_team_id UUID REFERENCES teams(id),
    away_team_id UUID REFERENCES teams(id),
    kickoff_time TIMESTAMP WITH TIME ZONE NOT NULL,
    home_score INTEGER,
    away_score INTEGER,
    status VARCHAR(50) DEFAULT 'SCHEDULED', -- SCHEDULED, IN_PLAY, FINISHED, POSTPONED, CANCELLED
    external_api_id INTEGER UNIQUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Picks table
CREATE TABLE picks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    gameweek_id UUID REFERENCES gameweeks(id) ON DELETE CASCADE,
    team_id UUID REFERENCES teams(id),
    points INTEGER DEFAULT 0,
    goals_for INTEGER DEFAULT 0,
    goals_against INTEGER DEFAULT 0,
    is_auto_assigned BOOLEAN DEFAULT false,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_user_gameweek UNIQUE (user_id, gameweek_id)
);

-- Team usage tracking table (to enforce pick-once-per-half rule)
CREATE TABLE team_selections (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    season_id UUID REFERENCES seasons(id) ON DELETE CASCADE,
    team_id UUID REFERENCES teams(id),
    half INTEGER NOT NULL, -- 1 for weeks 1-20, 2 for weeks 21-38
    gameweek_number INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_user_season_team_half UNIQUE (user_id, season_id, team_id, half),
    CONSTRAINT valid_half CHECK (half IN (1, 2))
);

-- Email notifications log
CREATE TABLE email_notifications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    gameweek_id UUID REFERENCES gameweeks(id) ON DELETE CASCADE,
    email_type VARCHAR(50) NOT NULL, -- PICK_REMINDER, GAMEWEEK_STARTED, etc.
    sent_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(50) DEFAULT 'SENT', -- SENT, FAILED, PENDING
    error_message TEXT
);

-- Admin actions audit log
CREATE TABLE admin_actions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    admin_user_id UUID REFERENCES users(id),
    action_type VARCHAR(100) NOT NULL, -- OVERRIDE_PICK, OVERRIDE_DEADLINE, DEACTIVATE_USER, etc.
    target_user_id UUID REFERENCES users(id),
    target_gameweek_id UUID REFERENCES gameweeks(id),
    details JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for performance
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_google_id ON users(google_id);
CREATE INDEX idx_users_active ON users(is_active);
CREATE INDEX idx_seasons_active ON seasons(is_active);
CREATE INDEX idx_gameweeks_season ON gameweeks(season_id);
CREATE INDEX idx_gameweeks_deadline ON gameweeks(deadline);
CREATE INDEX idx_fixtures_gameweek ON fixtures(gameweek_id);
CREATE INDEX idx_fixtures_status ON fixtures(status);
CREATE INDEX idx_picks_user ON picks(user_id);
CREATE INDEX idx_picks_gameweek ON picks(gameweek_id);
CREATE INDEX idx_team_selections_user_season ON team_selections(user_id, season_id);
CREATE INDEX idx_email_notifications_user ON email_notifications(user_id);

-- Function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers to auto-update updated_at
CREATE TRIGGER update_users_updated_at BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_seasons_updated_at BEFORE UPDATE ON seasons FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_teams_updated_at BEFORE UPDATE ON teams FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_gameweeks_updated_at BEFORE UPDATE ON gameweeks FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_fixtures_updated_at BEFORE UPDATE ON fixtures FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_picks_updated_at BEFORE UPDATE ON picks FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
