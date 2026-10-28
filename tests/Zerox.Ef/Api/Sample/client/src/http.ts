// Every generated call goes through here: the server's address comes from SAMPLE_URL.
export const http = async <T>(url: string, init: RequestInit): Promise<T> => {
  const base = process.env['SAMPLE_URL'];
  if (!base) throw new Error('SAMPLE_URL is not set');
  const response = await fetch(new URL(url, base), init);
  const text = await response.text();
  const data: unknown = text ? JSON.parse(text) : undefined;
  return { data, status: response.status, headers: response.headers } as T;
};
