import { createContext, useContext, useEffect, useState, useCallback } from 'react';
import type { ReactNode } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from './AuthContext';
import { API_URL } from '@/config/constants';

interface SignalRContextType {
  connection: signalR.HubConnection | null;
  isConnected: boolean;
  onSeasonApprovalUpdate: (callback: (data: SeasonApprovalUpdateData) => void) => void;
  offSeasonApprovalUpdate: (callback: (data: SeasonApprovalUpdateData) => void) => void;
  onResultsUpdated: (callback: (data: ResultsUpdateData) => void) => void;
  offResultsUpdated: (callback: (data: ResultsUpdateData) => void) => void;
  onAutoPickAssigned: (callback: (data: AutoPickAssignedData) => void) => void;
  offAutoPickAssigned: (callback: (data: AutoPickAssignedData) => void) => void;
  onSeasonCreated: (callback: (data: SeasonCreatedData) => void) => void;
  offSeasonCreated: (callback: (data: SeasonCreatedData) => void) => void;
  subscribeToResults: () => void;
  unsubscribeFromResults: () => void;
}

export interface SeasonApprovalUpdateData {
  isApproved: boolean;
  seasonName: string;
  timestamp: string;
}

export interface ResultsUpdateData {
  fixturesUpdated: number;
  picksRecalculated: number;
  gameweeksProcessed: number;
  updatedFixtures: Array<{
    fixtureId: string;
    gameweekNumber: number;
    homeTeam: string;
    awayTeam: string;
    homeScore: number | null;
    awayScore: number | null;
    status: string;
  }>;
  message: string;
  timestamp: string;
}

export interface AutoPickAssignedData {
  teamName: string;
  gameweekNumber: number;
  message: string;
  timestamp: string;
}

export interface SeasonCreatedData {
  seasonId: string;
  seasonName: string;
  message: string;
}

const SignalRContext = createContext<SignalRContextType | undefined>(undefined);

export function SignalRProvider({ children }: { children: ReactNode }) {
  const { token, isAuthenticated } = useAuth();
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    if (!isAuthenticated || !token) {
      // Disconnect if not authenticated
      if (connection) {
        connection.stop();
        setConnection(null);
        setIsConnected(false);
      }
      return;
    }

    // Create connection
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/notifications`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Start connection
    newConnection
      .start()
      .then(() => {
        console.log('SignalR Connected');
        setIsConnected(true);
      })
      .catch((err) => {
        console.error('SignalR Connection Error: ', err);
        setIsConnected(false);
      });

    // Handle reconnection
    newConnection.onreconnecting((error) => {
      console.log('SignalR Reconnecting...', error);
      setIsConnected(false);
    });

    newConnection.onreconnected((connectionId) => {
      console.log('SignalR Reconnected:', connectionId);
      setIsConnected(true);
    });

    newConnection.onclose((error) => {
      console.log('SignalR Connection Closed', error);
      setIsConnected(false);
    });

    setConnection(newConnection);

    // Cleanup on unmount
    return () => {
      newConnection.stop();
    };
  }, [isAuthenticated, token]);

  const onSeasonApprovalUpdate = useCallback(
    (callback: (data: SeasonApprovalUpdateData) => void) => {
      if (connection) {
        connection.on('SeasonApprovalUpdate', callback);
      }
    },
    [connection]
  );

  const offSeasonApprovalUpdate = useCallback(
    (callback: (data: SeasonApprovalUpdateData) => void) => {
      if (connection) {
        connection.off('SeasonApprovalUpdate', callback);
      }
    },
    [connection]
  );

  const onResultsUpdated = useCallback(
    (callback: (data: ResultsUpdateData) => void) => {
      if (connection) {
        connection.on('ResultsUpdated', callback);
      }
    },
    [connection]
  );

  const offResultsUpdated = useCallback(
    (callback: (data: ResultsUpdateData) => void) => {
      if (connection) {
        connection.off('ResultsUpdated', callback);
      }
    },
    [connection]
  );

  const subscribeToResults = useCallback(() => {
    if (connection && isConnected) {
      connection.invoke('SubscribeToResults').catch((err) => {
        console.error('Error subscribing to results:', err);
      });
    }
  }, [connection, isConnected]);

  const unsubscribeFromResults = useCallback(() => {
    if (connection && isConnected) {
      connection.invoke('UnsubscribeFromResults').catch((err) => {
        console.error('Error unsubscribing from results:', err);
      });
    }
  }, [connection, isConnected]);

  const onAutoPickAssigned = useCallback(
    (callback: (data: AutoPickAssignedData) => void) => {
      if (connection) {
        connection.on('AutoPickAssigned', callback);
      }
    },
    [connection]
  );

  const offAutoPickAssigned = useCallback(
    (callback: (data: AutoPickAssignedData) => void) => {
      if (connection) {
        connection.off('AutoPickAssigned', callback);
      }
    },
    [connection]
  );

  const onSeasonCreated = useCallback(
    (callback: (data: SeasonCreatedData) => void) => {
      if (connection) {
        connection.on('SeasonCreated', callback);
      }
    },
    [connection]
  );

  const offSeasonCreated = useCallback(
    (callback: (data: SeasonCreatedData) => void) => {
      if (connection) {
        connection.off('SeasonCreated', callback);
      }
    },
    [connection]
  );

  const value: SignalRContextType = {
    connection,
    isConnected,
    onSeasonApprovalUpdate,
    offSeasonApprovalUpdate,
    onResultsUpdated,
    offResultsUpdated,
    onAutoPickAssigned,
    offAutoPickAssigned,
    onSeasonCreated,
    offSeasonCreated,
    subscribeToResults,
    unsubscribeFromResults,
  };

  return <SignalRContext.Provider value={value}>{children}</SignalRContext.Provider>;
}

export function useSignalR() {
  const context = useContext(SignalRContext);
  if (context === undefined) {
    throw new Error('useSignalR must be used within a SignalRProvider');
  }
  return context;
}
