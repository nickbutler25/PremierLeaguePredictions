import { apiClient } from './api';

export interface UserListItem {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  isAdmin: boolean;
  isPaid: boolean;
  createdAt: string;
}

export const usersService = {
  async getUsers(): Promise<UserListItem[]> {
    const response = await apiClient.get<UserListItem[]>('/api/users');
    return response.data;
  },

  async getUser(userId: string): Promise<UserListItem> {
    const response = await apiClient.get<UserListItem>(`/api/users/${userId}`);
    return response.data;
  },
};
