import { AxiosError } from 'axios';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * Extracts a human-readable message from an API error (RFC 7807 ProblemDetails),
 * falling back to the provided default.
 */
export function extractProblemMessage(error: unknown, fallback: string): string {
  if (error instanceof AxiosError) {
    const data = error.response?.data as ProblemDetails | undefined;
    if (data?.detail) return data.detail;
    if (data?.errors) {
      const first = Object.values(data.errors).flat()[0];
      if (first) return first;
    }
    if (data?.title) return data.title;
    if (error.response?.status === 403) return 'You do not have permission to do this.';
  }
  return fallback;
}
