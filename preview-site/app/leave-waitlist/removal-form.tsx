'use client';
import Link from 'next/link';
import { useState } from 'react';
export function RemovalForm() {
  const [state, setState] = useState<'idle' | 'sending' | 'done' | 'error'>(
    'idle',
  );
  const [error, setError] = useState('');
  async function submit(event: {
    preventDefault: () => void;
    currentTarget: HTMLFormElement;
  }) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setState('sending');
    try {
      const response = await fetch('/api/waitlist/remove', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: data.get('email'),
          website: data.get('website'),
        }),
      });
      const result = (await response.json()) as { error?: string };
      if (!response.ok) throw new Error(result.error || 'Please try again.');
      setState('done');
    } catch (e) {
      setState('error');
      setError(e instanceof Error ? e.message : 'Please try again.');
    }
  }
  return state === 'done' ? (
    <div className="removal-success" aria-live="polite">
      <h2>Removal complete.</h2>
      <p>
        If that address was on the waiting list, its signup record has been
        removed.
      </p>
      <Link href="/">Back to Hallzee</Link>
    </div>
  ) : (
    <form className="removal-form" onSubmit={submit}>
      <label htmlFor="remove-email">Your email address</label>
      <input
        type="email"
        id="remove-email"
        name="email"
        required
        maxLength={254}
        autoComplete="email"
        disabled={state === 'sending'}
        aria-describedby="remove-note remove-error"
      />
      <p id="remove-note">
        Use the same address you signed up with. Only remove an address you
        control.
      </p>
      <div className="honeypot" aria-hidden="true">
        <label htmlFor="remove-website">Leave this empty</label>
        <input
          id="remove-website"
          name="website"
          type="text"
          tabIndex={-1}
          autoComplete="off"
        />
      </div>
      <button
        className="button button-primary"
        type="submit"
        disabled={state === 'sending'}
      >
        {state === 'sending' ? 'Removing…' : 'Remove my email'}
      </button>
      <p id="remove-error" role="alert">
        {state === 'error' ? error : ''}
      </p>
    </form>
  );
}
