---
name: artel-test-cases
description: >-
  Write, revise, or review ARTEL automated QA test cases against a target game build.
  Use for ARTEL TC authoring, QA feasibility reviews, and deciding whether an existing
  or small test-only cheat or behavior can make a case runnable. Covers ARTEL QA cases,
  including 테스트 케이스 작성 and TC 검토, rather than Unity EditMode or PlayMode tests.
---

# ARTEL automated QA test cases

As the coding agent, author cases and review them before saving. The QA agent runs
them in the game build. Base each case on the controls and evidence available to
the QA agent, even when you can see more in the source.

## Candidate selection

1. Identify the project and target build. Review existing cases and state names;
   reuse a matching case instead of duplicating it. Use ARTEL's `authoring_guide`
   for case format and `test_case_guide` when choosing SDK `observe` values.
2. Take the intended result from the user's request or spec. Use code and build
   evidence to establish these three parts:
   - **Reach:** Identify the starting screen and a repeatable way to meet its
     preconditions.
   - **Act:** Name the player input, macro, or behavior the QA agent can actually
     use. For an automatic event with a lasting result, specify when to start
     observing and when to check the result.
   - **Judge:** Name the visible result or readable SDK value, when to read it,
     and what separates pass from fail. Where other actions or targets could
     produce the same evidence, tie it to the intended action and target.
3. Keep the specified expected result when code suggests a defect. Flag the
   discrepancy. Keep any narrower, independently verifiable result in a
   separate case.

## Momentary events

Exclude short-lived events as standalone automated QA cases when their verdict
depends on catching the event itself. Examples include a projectile crossing the
screen, a brief hit effect, and a short animation. Assume point-in-time screenshots;
do not rely on continuous screen observation. List these as manual checks when
coverage is needed. A lasting result may be a separate case if the QA agent can
attribute it to the action and target. Report only the outcome that case checks.

## Before-and-after comparisons

State how the QA agent obtains both the baseline and the comparison. For actual
damage, identify the attack source, the target, and the damage applied by that hit.
Match or account for target defense, critical hits, and buffs. A displayed attack
stat or a combined damage total cannot establish the applied damage of a specific
hit. If the build exposes only the stat, cover it as a separate UI case and defer
the damage case.

## Test support

Reuse existing controls, observations, macros, cheats, and behaviors first.
Add a small, localized cheat or behavior within existing test support if it can
unblock a case without changing core gameplay. Follow the `artel-cheats` or
`artel-behaviors` skill for implementation.

- Use cheats to prepare the starting state. Keep the action under test and its
  outcome on the real gameplay path.
- Use behaviors for timed or repeated inputs. Preserve the actual input path
  when the input binding itself is under test. Judge the game result separately
  from the tool's success response.
- Confirm new test support is registered and works in the target development
  build before relying on it. Use `list_game_functions` when available to check
  registration.

New practice arenas, dedicated targets and reset systems, shared recording or
telemetry infrastructure, new dependencies, and core gameplay changes require
an explicit user request. Defer the affected cases and explain the missing
evidence while that work is outside scope.

## Review before saving

Review the draft separately from authoring. Using only what the QA agent can
access, walk through the starting state, action, and verdict. Check negative
cases for evidence that the game received the attempt; an unchanged state
alone may also mean the agent missed the input.

- **Authorable:** There is evidence for reach, act, and judge in the target build.
- **Needs build verification:** The path is plausible but the action or evidence
  has not been confirmed for that build. Name the check still needed.
- **Deferred:** An action, starting condition, or observation is missing, or
  the case depends on a momentary event. Record why and, where useful, suggest
  manual coverage or the minimum support that would unblock it.

If asked to save, submit only authorable cases through the existing ARTEL
workflow. Report the other candidates and reasons separately. Authorability,
console review status, and an actual QA verdict are separate results.

Read [references/few-shot.md](references/few-shot.md) for examples when applying
these rules to timing, blocked actions, mixed damage, or test support decisions.
