import { getDatabase } from '@/db';
import { readWaitlistRequest } from '@/lib/waitlist-request';
export async function POST(request: Request) {
  try {
    const data = await readWaitlistRequest(request);
    if (data instanceof Response) return data;
    if (!data.bot)
      await getDatabase()
        .prepare('DELETE FROM waitlist WHERE email = ?')
        .bind(data.email)
        .run();
    // The same response for present/absent addresses keeps list membership private.
    return Response.json(
      { ok: true },
      { headers: { 'Cache-Control': 'no-store' } },
    );
  } catch {
    return Response.json(
      {
        error:
          'We couldn’t complete the removal. Please try again in a moment.',
      },
      { status: 503 },
    );
  }
}
