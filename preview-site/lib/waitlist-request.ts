export async function readWaitlistRequest(
  request: Request,
): Promise<{ email: string; bot: boolean } | Response> {
  const error = (message: string, status: number) =>
    Response.json({ error: message }, { status });
  const origin = request.headers.get('origin');
  try {
    if (origin && new URL(origin).host !== new URL(request.url).host)
      return error('Please submit the form from the Hallzee website.', 403);
  } catch {
    return error('Please submit the form from the Hallzee website.', 403);
  }
  if (!request.headers.get('content-type')?.includes('application/json'))
    return error('Please use the form on the Hallzee website.', 415);
  if (Number(request.headers.get('content-length') || 0) > 2048)
    return error('The request is too large.', 413);
  const raw = await request.text();
  if (raw.length > 2048) return error('The request is too large.', 413);
  let data: { email?: unknown; website?: unknown };
  try {
    data = JSON.parse(raw);
  } catch {
    return error('Please enter a valid email address.', 400);
  }
  if (!data || typeof data !== 'object')
    return error('Please enter a valid email address.', 400);
  if (data.website) return { email: '', bot: true };
  const email =
    typeof data.email === 'string' ? data.email.trim().toLowerCase() : '';
  let hasControl = false;
  for (let i = 0; i < email.length; i++)
    if (email.charCodeAt(i) < 32) hasControl = true;
  if (
    email.length > 254 ||
    hasControl ||
    !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
  )
    return error('Please enter a valid email address.', 400);
  return { email, bot: false };
}
