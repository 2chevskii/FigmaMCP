---
name: repository-commits
description: Prepare and create safe, logically grouped Git commits with Conventional Commit messages. Use when the user explicitly asks to commit repository changes; do not use for pushes or pull requests.
---

# Repository commits

Create commits only after the user explicitly requests them. Do not push, amend, reset, or rewrite
history unless separately requested.

## Inspect and group

Before staging, inspect the whole working tree and existing staging area:

```powershell
rtk git status --short
rtk git diff --name-status
rtk git diff --cached --name-status
rtk git diff --check
rtk git log -5 --format="%h %s"
```

If RTK cannot start because its local configuration is unavailable, invoke the same `git` commands
directly and report the fallback.

Group changes before staging. A group is one reviewable, independently understandable change; keep
its implementation, directly supporting tests, and required documentation together. Split a large
set when changes differ in purpose, area, or intended Conventional Commit type. Also separate
otherwise unrelated batches made at clearly different times: use file modification times as a
signal, then confirm the grouping from the diff. Do not split files merely to make commits smaller.

Treat pre-staged changes as user state. Review them first; do not unstage or overwrite them. If their
intended grouping cannot be determined from the diff and request, ask the user before committing.

## Commit each group

1. State the planned groups and their messages when the grouping is non-obvious.
2. Stage only the explicit paths for one group with `git add -- <paths>`; do not use broad staging
   commands such as `git add -A` for a mixed working tree.
3. Review `git diff --cached` and `git diff --cached --check`. Run the relevant existing verification
   commands for the group when appropriate.
4. Commit with a Conventional Commit message:

   ```text
   <type>(<optional scope>): <concise imperative description>
   ```

   Use the type that expresses the change: `feat`, `fix`, `docs`, `refactor`, `test`, `build`, `ci`,
   or `chore`. Use an optional scope only when it makes the subject clearer. Keep the subject specific
   enough to understand from `git log` without opening the diff.
5. Confirm the created commit with `git show --stat --oneline HEAD`, then re-check `git status --short`
   before moving to the next group.

If there are no changes, do not create an empty commit. If a commit or verification fails, stop and
report the failure instead of bypassing hooks or changing unrelated files.
