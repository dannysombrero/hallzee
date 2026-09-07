import { env } from 'cloudflare:workers';
import { getDatabase } from '@/db';

type ExportEnv = { WAITLIST_EXPORT_TOKEN?: string };
export async function GET(request: Request) {
  const secret = (env as ExportEnv).WAITLIST_EXPORT_TOKEN;
  const authorization = request.headers.get('authorization');
  if (!secret || authorization !== `Bearer ${secret}`)
    return new Response('Unauthorized', {
      status: 401,
      headers: { 'Cache-Control': 'no-store' },
    });
  const { results } = await getDatabase()
    .prepare(
      'SELECT email, joined_at, notice_version FROM waitlist ORDER BY joined_at DESC',
    )
    .all<{ email: string; joined_at: string; notice_version: string | null }>();
  const cell = (s: string) =>
    `"${s.replace(/^[=+\-@\t\r]/, "'$&").replace(/"/g, '""')}"`;
  const csv =
    [
      'email,joined_at,notice_version',
      ...results.map(
        (r) =>
          `${cell(r.email)},${cell(r.joined_at)},${cell(r.notice_version || '')}`,
      ),
    ].join('\r\n') + '\r\n';
  return new Response(csv, {
    headers: {
      'Content-Type': 'text/csv; charset=utf-8',
      'Content-Disposition': 'attachment; filename="hallzee-waiting-list.csv"',
      'Cache-Control': 'no-store',
    },
  });
}
