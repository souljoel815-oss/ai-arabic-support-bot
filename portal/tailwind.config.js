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
                'brand-glow': '0 6px 24px -8px rgba(176, 109, 24, 0.35)',
            },
        },
    },

    plugins: [forms],
};
