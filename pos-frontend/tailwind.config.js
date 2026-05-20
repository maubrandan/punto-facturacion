/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        primary: 'var(--color-primary)',
        'primary-50': 'var(--color-primary-50)',
        'primary-100': 'var(--color-primary-100)',
        'primary-300': 'var(--color-primary-300)',
        'primary-400': 'var(--color-primary-400)',
        'primary-500': 'var(--color-primary-500)',
        'primary-700': 'var(--color-primary-700)',
        accent: 'var(--color-accent)',
        'accent-400': 'var(--color-accent-400)',
        'accent-500': 'var(--color-accent-500)',
        surface: {
          base: '#020617',
          card: 'rgb(15 23 42 / 0.5)',
          elevated: 'rgb(30 41 59 / 0.6)'
        }
      }
    }
  }
};
