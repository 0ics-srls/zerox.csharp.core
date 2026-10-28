import { defineConfig } from 'eslint/config';
import js from '@eslint/js';
import tseslint from 'typescript-eslint';

export default defineConfig(
  { ignores: ['src/api/**', 'node_modules/**'] },
  js.configs.all,
  tseslint.configs.all,
  { languageOptions: { parserOptions: { projectService: { allowDefaultProject: ['*.js', '*.mjs'] } } } },
  {
    rules: {
      '@typescript-eslint/naming-convention': 'off',
      '@typescript-eslint/prefer-readonly-parameter-types': 'off',
      'capitalized-comments': 'off',
      complexity: 'off',
      'max-depth': 'off',
      'max-lines': 'off',
      'max-lines-per-function': 'off',
      'max-nested-callbacks': 'off',
      'max-params': 'off',
      '@typescript-eslint/max-params': 'off',
      'max-statements': 'off',
      'no-ternary': 'off',
      'no-undefined': 'off',
      'one-var': 'off',
      'sort-vars': 'off',
      'sort-keys': 'off',
    },
  },
);
