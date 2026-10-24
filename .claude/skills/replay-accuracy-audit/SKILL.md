---
name: replay-accuracy-audit
description: Use in a new session when invoked as /replay-accuracy-audit <branch>, or when asked to check a <hero>-accuracy-v<N> branch against its spec. Stage 4 of docs/REPLAY_ACCURACY.md - a spec-conformance-auditor agent compares the branch with docs/accuracy/<branch>.md as first committed, and the session writes docs/accuracy/<branch>.audit.md.
argument-hint: <hero>-accuracy-v<N>
---

# Spec audit of a replay accuracy branch

The argument is a branch, `<hero>-accuracy-v<N>`, with its SimFell worktree at `G:/source/SimFell.<branch>`. The
session runs on Opus 5.5 at medium effort; the auditor runs on Opus 5.5 at high effort, by its definition.

1. **The spec as written.** The commit that added `docs/accuracy/<branch>.md`:

   ```
   git -C G:/source/SimFell.<branch> log --diff-filter=A --format=%H -- docs/accuracy/<branch>.md
   ```

   Each later commit touching the spec is listed with its message.

2. **The auditor.** A `spec-conformance-auditor` agent gets the worktree path, the branch, the spec's commit and the
   later spec commits. Its prompt has nothing else: no review file, no pull request body, no summary of the work.

3. **The audit file.** The agent's answer is `docs/accuracy/<branch>.audit.md`. The session commits it on the branch
   and pushes it, with no attribution lines, and reports to the user: items met, items not met, items under "Not yet
   explained", changes outside the spec, and spec changes after its first commit.
