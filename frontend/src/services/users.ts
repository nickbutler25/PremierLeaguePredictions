import { apiClient } from './api';
import type { ApiResponse } from '@/types';

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
    const response = await apiClient.get<ApiResponse<UserListItem[]>>('/api/v1/users');
    return response.data.data!;
  },

  async getUser(userId: string): Promise<UserListItem> {
    const response = await apiClient.get<ApiResponse<UserListItem>>(`/api/users/${userId}`);
    return response.data.data!;
  },
};
