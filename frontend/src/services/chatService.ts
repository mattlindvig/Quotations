import apiClient from './apiClient';
import type { Quotation } from '../types/quotation';

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatResponse {
  reply: string;
  quotations: Quotation[];
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

export async function sendChatMessage(
  message: string,
  conversationHistory: ChatMessage[]
): Promise<ChatResponse> {
  const response = await apiClient.post<ApiResponse<ChatResponse>>('/chat', {
    message,
    conversationHistory
  });
  return response.data;
}
