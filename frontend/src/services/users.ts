import { apiClient } from './api';
import type { ApiResponse, User, UpdateUserRequest } from '@/types';

export interface UserListItem {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isAdmin: boolean;
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

  async updateUser(userId: string, data: UpdateUserRequest): Promise<User> {
    const response = await apiClient.put<ApiResponse<User>>(`/api/v1/users/${userId}`, data);
    return response.data.data!;
  },

  async updateTheme(theme: 'light' | 'dark'): Promise<User> {
    const response = await apiClient.put<ApiResponse<User>>('/api/v1/users/me/theme', { theme });
    return response.data.data!;
  },

  async uploadPhoto(file: File): Promise<User> {
    const formData = new FormData();
    formData.append('file', file);
    // Override the client's default JSON content-type. Without this, axios sees a
    // FormData body with an application/json header and serialises it to JSON
    // instead of a real multipart upload (server then 415s). Setting multipart/form-data
    // lets the browser fill in the boundary.
    const response = await apiClient.post<ApiResponse<User>>('/api/v1/users/me/photo', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data.data!;
  },

  async deletePhoto(): Promise<User> {
    const response = await apiClient.delete<ApiResponse<User>>('/api/v1/users/me/photo');
    return response.data.data!;
  },

  // --- Admin-only operations (require Admin role) ---

  async updateAdmin(userId: string, isAdmin: boolean): Promise<User> {
    const response = await apiClient.patch<ApiResponse<User>>(`/api/v1/users/${userId}/admin`, {
      isAdmin,
    });
    return response.data.data!;
  },

  async deleteUser(userId: string): Promise<void> {
    await apiClient.delete(`/api/v1/users/${userId}`);
  },
};
