# Git

> **Bootstrap'te onaylandı (2026-10-26).** Dal ve commit kuralları şablondaki güvenli
> varsayılanlarla aynı bırakıldı; yalnızca "Trivial changes" politikası seçildi.
> Varsayılan dal: `main`. Commit mesajı dili Türkçe, önekler İngilizce (`docs/conventions.md`).

## Branching
- `feature/<spec-no>-<short-name>` — **no branch without a spec.** Also for CHANGE-lane mini-specs.
- Refactors: `refactor/<spec-no>-<short-name>` (mini-spec with Changed behavior = none).
- Fixes: `fix/<report-id>-<short-name>` (bug fixes carry a report, not a spec number);
  incidents: `incident/<date>-<short-name>`; trivial changes: `trivial/<short-name>` (see below).

## Commits
- Conventional Commits, with a plan reference: `feat(catalog): paging endpoint [plan 0001/3]`.
  Review fixes name their fix round instead of the step:
  `fix(catalog): empty page size [plan 0001/fix-2]` — the reviewer counts rounds from these
  (R-06 after round 3).
- Agent commits follow the same standard: the agent writes the message, the human approves.

## Forbidden
- Direct commits to the default branch.
- Force push, history rewriting on shared branches. Undo = `git revert` (see recovery R-11).

## Trivial changes
<!-- Set at bootstrap. Applies only to requests the change-request triage rubric
     (workflows/change-request.md) classifies as TRIVIAL: no acceptance criterion changes, no shared code. -->
- Policy: **(2) `trivial/<short-name>` dalında doğrudan commit serbest** — bootstrap'te seçildi;
  PR ve spec gerekmez. (Seçilmeyen alternatifler: (1) PR + bir reviewer, spec yok ·
  (3) her şey mini-spec'ten geçer, `specs/TEMPLATE-mini.md`.)
- Always: one narrow commit, `scripts/check` green. A second file or a new test means re-triage.

## Pull requests
- PR template checklist completed; `scripts/check` green in CI; squash-merge.
