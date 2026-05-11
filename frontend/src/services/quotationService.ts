import apiClient from './apiClient';
import type { Quotation, ApiResponse, SourceType } from '../types/quotation';

export async function getQuoteOfTheDay(): Promise<Quotation | null> {
  try {
    const res = await apiClient.get<ApiResponse<Quotation>>('/quotations/quote-of-the-day');
    return res.data ?? null;
  } catch {
    return null;
  }
}

export async function getRandomBatch(
  count: number = 12,
  sourceType?: SourceType,
  tags?: string[]
): Promise<Quotation[]> {
  const params = new URLSearchParams({ count: count.toString() });
  if (sourceType) params.set('sourceType', sourceType);
  if (tags && tags.length > 0) params.set('tags', tags.join(','));

  const res = await apiClient.get<ApiResponse<Quotation[]>>(`/quotations/random-batch?${params}`);
  return res.data ?? [];
}
