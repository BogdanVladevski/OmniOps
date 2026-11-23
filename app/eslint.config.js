// https://docs.expo.dev/guides/using-eslint/
const { defineConfig } = require('eslint/config');
const expoConfig = require('eslint-config-expo/flat');

module.exports = defineConfig([
  expoConfig,
  {
    ignores: ['dist/*', 'src/api/schema.d.ts'],
  },
  {
    rules: {
      'no-console': ['warn', { allow: ['warn', 'error'] }],
      'react/jsx-no-useless-fragment': 'warn',
    },
  },
  {
    // jest.mock factories are hoisted above imports, so they can only use require().
    files: ['**/*.test.ts', '**/*.test.tsx', 'jest.setup.ts'],
    rules: { '@typescript-eslint/no-require-imports': 'off' },
  },
]);
