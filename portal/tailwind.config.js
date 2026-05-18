import defaultTheme from 'tailwindcss/defaultTheme';
import forms from '@tailwindcss/forms';

/** @type {import('tailwindcss').Config} */
export default {
    content: [
        './vendor/laravel/framework/src/Illuminate/Pagination/resources/views/*.blade.php',
        './storage/framework/views/*.php',
        './resources/views/**/*.blade.php',
    ],

    theme: {
        extend: {
            colors: {
                // DaftarX brand palette — gold + charcoal. Pulled into
                // the design token namespace so every screen + component
                // references brand.* instead of hard-coded amber/stone
                // shades. Switching the palette later = edit this file.
                brand: {
                    50: '#fdf8ed',
                    100: '#faecca',
                    200: '#f5d691',
                    300: '#eebb58',
                    400: '#e9a234',     // accent (hover)
                    500: '#d68a1f',     // hero buttons
                    600: '#b06d18',     // primary CTAs
                    700: '#8b5616',     // pressed state
                    800: '#714617',
                    900: '#603b18',
                    950: '#371f0a',
                },
                ink: {
                    50: '#f6f5f3',
                    100: '#e8e5e1',
                    200: '#d2cdc4',
                    300: '#b6ad9f',
                    400: '#9b8e7c',
                    500: '#82735f',
                    600: '#6c5e4c',
                    700: '#544a3d',
                    800: '#403a30',
                    900: '#2d2a23',
                    950: '#1c1a16',      // headlines
                },
            },
            fontFamily: {
                sans: ['Cairo', 'Tajawal', 'Figtree', ...defaultTheme.fontFamily.sans],
                serif: ['Amiri', ...defaultTheme.fontFamily.serif],
            },
            boxShadow: {
                'brand-glow':    '0 6px 24px -8px rgba(176, 109, 24, 0.35)',
                'brand-glow-lg': '0 12px 32px -8px rgba(176, 109, 24, 0.45), 0 2px 6px -2px rgba(176, 109, 24, 0.2)',
                'elevation-1':   '0 1px 2px 0 rgba(28, 26, 22, 0.04), 0 1px 3px 0 rgba(28, 26, 22, 0.06)',
                'elevation-2':   '0 4px 6px -1px rgba(28, 26, 22, 0.06), 0 2px 4px -1px rgba(28, 26, 22, 0.04)',
                'elevation-3':   '0 10px 15px -3px rgba(28, 26, 22, 0.08), 0 4px 6px -2px rgba(28, 26, 22, 0.05)',
                'inset-line':    'inset 0 -1px 0 rgba(28, 26, 22, 0.06)',
            },
            keyframes: {
                'pulse-soft': {
                    '0%, 100%': { opacity: '1' },
                    '50%':      { opacity: '0.6' },
                },
            },
            animation: {
                'pulse-soft': 'pulse-soft 2.4s ease-in-out infinite',
            },
        },
    },

    plugins: [forms],
};
