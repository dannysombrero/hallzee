import { env } from 'cloudflare:workers';
export function getDatabase() {
  if (!env.DB) throw new Error('Waiting list storage is unavailable.');
  return env.DB;
}
