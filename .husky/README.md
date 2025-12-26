# Git Hooks (Husky + lint-staged)

This repository uses [Husky](https://typicode.github.io/husky/) and [lint-staged](https://github.com/lint-staged/lint-staged) to enforce code quality.

## What happens on commit?

When you run `git commit`, the pre-commit hook automatically:

1. **Formats code** - Runs Prettier on staged files
2. **Fixes linting issues** - Runs ESLint with auto-fix on staged files
3. **Only affects staged files** - Doesn't touch unstaged changes

## Pre-commit Hook

Located in `.husky/pre-commit`

Runs `lint-staged` on the frontend directory, which:
- Runs ESLint and Prettier on `.ts`, `.tsx`, `.js`, `.jsx` files
- Runs Prettier on `.json`, `.css`, `.md` files

## Setup for New Developers

If you clone this repo, the hooks are automatically set up because:
```bash
git config core.hooksPath .husky
```

This is already configured in the repository.

## Bypassing Hooks (Not Recommended)

If you absolutely need to skip hooks (debugging only):
```bash
git commit --no-verify
```

**Warning:** This bypasses all quality checks. Use sparingly!

## Troubleshooting

### Hook not running?
Check that the hooks path is configured:
```bash
git config core.hooksPath
# Should output: .husky
```

If not, run:
```bash
git config core.hooksPath .husky
```

### Permission denied?
Make the hook executable:
```bash
chmod +x .husky/pre-commit
```

### Lint-staged failing?
Test manually:
```bash
cd frontend
npx lint-staged
```
