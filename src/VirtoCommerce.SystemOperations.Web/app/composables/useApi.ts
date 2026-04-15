export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isPermissionDenied(): boolean {
    return this.status === 401 || this.status === 403;
  }
}

export function useApi() {
  async function request<T = unknown>(
    url: string,
    method: 'GET' | 'POST' = 'GET',
    body?: unknown,
  ): Promise<T> {
    const opts: RequestInit = { method, credentials: 'same-origin' };

    if (body !== undefined) {
      opts.headers = { 'Content-Type': 'application/json' };
      opts.body = JSON.stringify(body);
    }

    const res = await fetch(url, opts);

    if (res.status === 401 || res.status === 403) {
      throw new ApiError(res.status, 'You don\'t have permission to perform this action.');
    }

    if (!res.ok) {
      throw new ApiError(res.status, `API error: ${res.status} ${res.statusText}`);
    }

    const contentType = res.headers.get('content-type');
    if (contentType?.includes('application/json')) {
      return res.json() as Promise<T>;
    }

    return undefined as T;
  }

  function get<T = unknown>(url: string): Promise<T> {
    return request<T>(url, 'GET');
  }

  function post<T = unknown>(url: string, body?: unknown): Promise<T> {
    return request<T>(url, 'POST', body);
  }

  return { get, post };
}
