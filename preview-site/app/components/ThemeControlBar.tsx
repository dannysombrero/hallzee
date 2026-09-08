'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Sparkles, ExternalLink, ChevronDown, ChevronUp, Palette, Check } from 'lucide-react';

export type ThemeMode = 'editorial' | 'logo-vibrant' | 'client';
export type BackgroundPreset = 'waves' | 'canvas' | 'azure' | 'emerald';

interface ThemeControlBarProps {
  currentTheme: ThemeMode;
  onThemeChange: (theme: ThemeMode) => void;
  currentBg: BackgroundPreset;
  onBgChange: (bg: BackgroundPreset) => void;
}

export function ThemeHeaderToggle({
  currentTheme,
  onThemeChange,
}: {
  currentTheme: ThemeMode;
  onThemeChange: (theme: ThemeMode) => void;
}) {
  return (
    <div
      className="theme-header-pill"
      role="group"
      aria-label="Website Theme Mode"
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        background: '#edf3f6',
        padding: '3px',
        borderRadius: '9999px',
        border: '1px solid rgba(16, 58, 91, 0.12)',
        fontSize: '12px',
        fontWeight: 600,
        gap: '2px',
      }}
    >
      <button
        type="button"
        onClick={() => onThemeChange('editorial')}
        style={{
          border: 'none',
          padding: '6px 12px',
          borderRadius: '9999px',
          cursor: 'pointer',
          background: currentTheme === 'editorial' ? '#ffffff' : 'transparent',
          color: currentTheme === 'editorial' ? '#172c34' : '#65777f',
          boxShadow: currentTheme === 'editorial' ? '0 2px 6px rgba(0, 0, 0, 0.08)' : 'none',
          transition: 'all 0.15s ease',
          fontFamily: 'inherit',
          display: 'inline-flex',
          alignItems: 'center',
          gap: '5px',
        }}
        aria-pressed={currentTheme === 'editorial'}
      >
        <span>Original</span>
      </button>

      <button
        type="button"
        onClick={() => onThemeChange('logo-vibrant')}
        style={{
          border: 'none',
          padding: '6px 12px',
          borderRadius: '9999px',
          cursor: 'pointer',
          background:
            currentTheme === 'logo-vibrant'
              ? 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)'
              : 'transparent',
          color: currentTheme === 'logo-vibrant' ? '#ffffff' : '#16324a',
          boxShadow:
            currentTheme === 'logo-vibrant'
              ? '0 3px 10px rgba(2, 132, 199, 0.4)'
              : 'none',
          transition: 'all 0.15s ease',
          fontFamily: 'inherit',
          fontWeight: 700,
          display: 'inline-flex',
          alignItems: 'center',
          gap: '6px',
        }}
        aria-pressed={currentTheme === 'logo-vibrant'}
      >
        <span
          style={{
            display: 'inline-block',
            width: '8px',
            height: '8px',
            borderRadius: '50%',
            background: 'linear-gradient(135deg, #76dc28, #14b8a6)',
          }}
        />
        <span>Logo Colors</span>
      </button>

      <button
        type="button"
        onClick={() => onThemeChange('client')}
        style={{
          border: 'none',
          padding: '6px 12px',
          borderRadius: '9999px',
          cursor: 'pointer',
          background:
            currentTheme === 'client'
              ? 'linear-gradient(180deg, #00b8f2 0%, #0098e8 45%, #0070c4 100%)'
              : 'transparent',
          color: currentTheme === 'client' ? '#ffffff' : '#65777f',
          boxShadow:
            currentTheme === 'client'
              ? '0 4px 10px rgba(0, 168, 236, 0.35)'
              : 'none',
          transition: 'all 0.15s ease',
          fontFamily: currentTheme === 'client' ? "'Nunito', sans-serif" : 'inherit',
          fontWeight: 700,
          display: 'inline-flex',
          alignItems: 'center',
          gap: '5px',
        }}
        aria-pressed={currentTheme === 'client'}
      >
        <Sparkles size={13} />
        <span>Desktop App</span>
      </button>
    </div>
  );
}

export function ThemeFloatingDock({
  currentTheme,
  onThemeChange,
  currentBg,
  onBgChange,
}: ThemeControlBarProps) {
  const [expanded, setExpanded] = useState(true);

  const bgOptions: { id: BackgroundPreset; label: string; icon: string }[] = [
    { id: 'waves', label: 'Wave Mesh', icon: '🌊' },
    { id: 'canvas', label: 'Slate Canvas', icon: '🎨' },
    { id: 'azure', label: 'Azure Wave', icon: '💎' },
    { id: 'emerald', label: 'Emerald Wave', icon: '🍃' },
  ];

  const getThemeBadge = () => {
    switch (currentTheme) {
      case 'logo-vibrant':
        return { label: 'LOGO PALETTE', bg: '#dcfce7', color: '#15803d' };
      case 'client':
        return { label: 'DESKTOP APP', bg: '#e0f4fc', color: '#0070c4' };
      default:
        return { label: 'ORIGINAL', bg: '#f0f4f6', color: '#65777f' };
    }
  };

  const badge = getThemeBadge();

  return (
    <aside
      aria-label="Theme Customizer"
      style={{
        position: 'fixed',
        bottom: '20px',
        right: '20px',
        zIndex: 9999,
        fontFamily: "Arial, Helvetica, sans-serif",
      }}
    >
      <div
        style={{
          background: 'rgba(255, 255, 255, 0.94)',
          backdropFilter: 'blur(20px)',
          WebkitBackdropFilter: 'blur(20px)',
          border: '1px solid rgba(2, 132, 199, 0.25)',
          borderRadius: '18px',
          boxShadow: '0 16px 40px -10px rgba(16, 58, 91, 0.22), 0 0 0 1px rgba(255, 255, 255, 0.8)',
          overflow: 'hidden',
          width: expanded ? '320px' : 'auto',
          transition: 'all 0.25s cubic-bezier(0.4, 0, 0.2, 1)',
        }}
      >
        {/* Header bar */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '12px 16px',
            background: 'linear-gradient(90deg, rgba(118, 220, 40, 0.08) 0%, rgba(2, 132, 199, 0.08) 100%)',
            borderBottom: expanded ? '1px solid rgba(220, 229, 236, 0.8)' : 'none',
            cursor: 'pointer',
          }}
          onClick={() => setExpanded(!expanded)}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Palette size={16} color="#0284c7" />
            <span
              style={{
                fontWeight: 700,
                fontSize: '14px',
                color: '#0c2b3e',
              }}
            >
              Theme Toggle
            </span>
            <span
              style={{
                background: badge.bg,
                color: badge.color,
                fontSize: '10px',
                fontWeight: 700,
                padding: '2px 8px',
                borderRadius: '9999px',
                letterSpacing: '0.04em',
              }}
            >
              {badge.label}
            </span>
          </div>
          <button
            type="button"
            aria-label={expanded ? 'Collapse panel' : 'Expand panel'}
            style={{
              background: 'transparent',
              border: 'none',
              color: '#5b6b7a',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              padding: 0,
            }}
          >
            {expanded ? <ChevronDown size={16} /> : <ChevronUp size={16} />}
          </button>
        </div>

        {/* Body content */}
        {expanded && (
          <div style={{ padding: '16px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
            {/* Version Options */}
            <div>
              <div
                style={{
                  fontSize: '11px',
                  fontWeight: 700,
                  letterSpacing: '0.06em',
                  color: '#65777f',
                  marginBottom: '8px',
                }}
              >
                SELECT DESIGN VERSION
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                {/* 1. Logo Vibrant */}
                <button
                  type="button"
                  onClick={() => onThemeChange('logo-vibrant')}
                  style={{
                    border:
                      currentTheme === 'logo-vibrant'
                        ? '1.5px solid #0284c7'
                        : '1px solid rgba(220, 229, 236, 0.9)',
                    background:
                      currentTheme === 'logo-vibrant'
                        ? 'linear-gradient(135deg, rgba(2, 132, 199, 0.08) 0%, rgba(118, 220, 40, 0.08) 100%)'
                        : '#ffffff',
                    padding: '9px 12px',
                    borderRadius: '10px',
                    cursor: 'pointer',
                    textAlign: 'left',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    transition: 'all 0.15s ease',
                  }}
                >
                  <div>
                    <div style={{ fontSize: '13px', fontWeight: 700, color: '#0c2b3e', display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span
                        style={{
                          width: '8px',
                          height: '8px',
                          borderRadius: '50%',
                          background: 'linear-gradient(135deg, #76dc28, #0284c7)',
                          display: 'inline-block',
                        }}
                      />
                      Logo Colors (Original Fonts)
                    </div>
                    <div style={{ fontSize: '11px', color: '#5b6b7a', marginTop: '2px' }}>
                      Original Arial font + vibrant Lime/Teal/Ocean logo colors
                    </div>
                  </div>
                  {currentTheme === 'logo-vibrant' && <Check size={16} color="#0284c7" />}
                </button>

                {/* 2. Original */}
                <button
                  type="button"
                  onClick={() => onThemeChange('editorial')}
                  style={{
                    border:
                      currentTheme === 'editorial'
                        ? '1.5px solid #16324a'
                        : '1px solid rgba(220, 229, 236, 0.9)',
                    background: currentTheme === 'editorial' ? '#f0f4f6' : '#ffffff',
                    padding: '9px 12px',
                    borderRadius: '10px',
                    cursor: 'pointer',
                    textAlign: 'left',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    transition: 'all 0.15s ease',
                  }}
                >
                  <div>
                    <div style={{ fontSize: '13px', fontWeight: 600, color: '#16324a' }}>
                      Original (As Is)
                    </div>
                    <div style={{ fontSize: '11px', color: '#65777f', marginTop: '2px' }}>
                      Unchanged paper white & muted original palette
                    </div>
                  </div>
                  {currentTheme === 'editorial' && <Check size={16} color="#16324a" />}
                </button>

                {/* 3. Desktop App Theme */}
                <button
                  type="button"
                  onClick={() => onThemeChange('client')}
                  style={{
                    border:
                      currentTheme === 'client'
                        ? '1.5px solid #00b8f2'
                        : '1px solid rgba(220, 229, 236, 0.9)',
                    background:
                      currentTheme === 'client'
                        ? 'rgba(0, 184, 242, 0.08)'
                        : '#ffffff',
                    padding: '9px 12px',
                    borderRadius: '10px',
                    cursor: 'pointer',
                    textAlign: 'left',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    transition: 'all 0.15s ease',
                  }}
                >
                  <div>
                    <div style={{ fontSize: '13px', fontWeight: 700, color: '#0070c4' }}>
                      Desktop Client Theme
                    </div>
                    <div style={{ fontSize: '11px', color: '#5b6b7a', marginTop: '2px' }}>
                      Nunito typography, pill buttons & frosted cards
                    </div>
                  </div>
                  {currentTheme === 'client' && <Check size={16} color="#0070c4" />}
                </button>
              </div>
            </div>

            {/* Background Sub-presets (only shown in Desktop Client Theme) */}
            {currentTheme === 'client' && (
              <div>
                <div
                  style={{
                    fontSize: '11px',
                    fontWeight: 700,
                    letterSpacing: '0.06em',
                    color: '#65777f',
                    marginBottom: '8px',
                  }}
                >
                  DESKTOP BACKGROUND
                </div>
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: '1fr 1fr',
                    gap: '6px',
                  }}
                >
                  {bgOptions.map((opt) => (
                    <button
                      key={opt.id}
                      type="button"
                      onClick={() => onBgChange(opt.id)}
                      style={{
                        border:
                          currentBg === opt.id
                            ? '1.5px solid #00b8f2'
                            : '1px solid rgba(220, 229, 236, 0.9)',
                        background:
                          currentBg === opt.id
                            ? 'rgba(0, 184, 242, 0.08)'
                            : 'rgba(255, 255, 255, 0.7)',
                        color: currentBg === opt.id ? '#0070c4' : '#16324a',
                        borderRadius: '8px',
                        padding: '7px 10px',
                        fontSize: '12px',
                        fontWeight: currentBg === opt.id ? 700 : 500,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        cursor: 'pointer',
                        transition: 'all 0.15s ease',
                      }}
                    >
                      <span>
                        {opt.icon} {opt.label}
                      </span>
                      {currentBg === opt.id && <Check size={13} color="#0070c4" />}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {/* Link to live desktop simulator */}
            <div style={{ borderTop: '1px solid rgba(220, 229, 236, 0.7)', paddingTop: '12px' }}>
              <Link
                href="/preview"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '8px',
                  background: 'rgba(16, 58, 91, 0.05)',
                  border: '1px solid rgba(16, 58, 91, 0.12)',
                  borderRadius: '10px',
                  padding: '9px 14px',
                  fontSize: '12px',
                  fontWeight: 600,
                  color: '#103a5b',
                  transition: 'all 0.15s ease',
                }}
              >
                <span>Launch Desktop App Simulator</span>
                <ExternalLink size={13} />
              </Link>
            </div>
          </div>
        )}
      </div>
    </aside>
  );
}
