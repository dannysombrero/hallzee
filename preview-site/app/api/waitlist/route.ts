import { getDatabase } from '@/db';
import { readWaitlistRequest } from '@/lib/waitlist-request';
export async function POST(request: Request) {
  try {
    const data = await readWaitlistRequest(request);
    if (data instanceof Response) return data;
    if (!data.bot)
      await getDatabase()
        .prepare(
          'INSERT INTO waitlist (email, joined_at, notice_version) VALUES (?, ?, ?) ON CONFLICT(email) DO NOTHING',
        )
        .bind(data.email, new Date().toISOString(), '2026-09-07')
        .run();
    return Response.json(
      { ok: true },
      { headers: { 'Cache-Control': 'no-store' } },
    );
  } catch {
    return Response.json(
      { error: 'Your email wasn’t saved. Please try again in a moment.' },
      { status: 503 },
    );
  }
}
