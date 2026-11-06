# Project conventions

- Do not introduce native bridge libraries or custom C ABI exports, including
  custom exports compiled into MNN itself. The user requires calling official
  MNN C++ APIs directly and clarified that no additional C++ code should be
  written. Managed C# interop and marshalling are allowed. Do not modify official
  MNN source code. The current Native~ bindings and MNN_* P/Invoke exports
  predate this stricter requirement and do not satisfy it; do not extend them or
  describe their test results as validation of direct official C++ calls.
- C# calls to C++ symbols require an explicitly supported compiler/platform ABI
  contract. Do not present hand-written STL layouts, private object offsets, or
  virtual table assumptions as a portable, officially supported MNN C# API.
- Organize native C# declarations by responsibility or inference task using
  `MNNInterop.*.cs` partial files. Keep library names and calling conventions
  shared, safe handles under `Runtime/Interop/Handles`, and managed APIs in
  `Runtime/Core`. Add task-specific files only for implemented native exports.
- Keep build artifacts, test logs, and temporary diagnostics within this project
  under `TestArtifacts~/`. Downloaded model assets belong under
  `Assets/StreamingAssets/MNN/Models/` so the user can reuse them in Studio;
  keep reports and dependencies under `TestArtifacts~/`. Do not download models
  or dependencies unnecessarily or scatter files elsewhere.
- Follow `Packages/com.eitan.mnn/Documentation~/ProjectStructure.md` for directory
  ownership and assembly boundaries. Put managed API code in the corresponding
  `Runtime/Core` domain and MonoBehaviours in `Runtime/Components`. Keep Editor
  and Player tests under `Tests/Editor` and `Tests/Runtime`, with shared helpers
  in their respective `Fixtures` folders. Standalone tooling belongs in `Tools~`.
- Preserve `.meta` GUIDs when moving Unity assets, keep public namespaces and
  assembly names stable, and follow `.editorconfig` and `.gitattributes`. Run
  `python3 Packages/com.eitan.mnn/Tools~/Validation/validate_package.py` after
  package layout changes; this check is not native inference validation.

## ub-mode

Apply these principles by default when writing or changing code in this project.
These principles also apply when the user writes `/ub-mode`.

Every change follows this order:

- First Make it Work.
- Then Make it Right.
- Follow Practice.

### First Make it Work

#### Laziness Protocol

- One decision lives in one place.
- Ship the smallest edit that solves the problem.

#### Foundational Thinking

- Get the data shape right before you write the logic.
- Do first whatever helps every later phase.
- If a concurrent writer would matter, do not share state.

#### TDD

- Write the failing test before you change the code.
- See it fail for the reason you expect.
- Make the smallest change that makes it pass.
- If an honest test is impractical, do not write a bad one.

#### Redesign From First Principles

- Do not bolt a new requirement onto the old design.
- Ask what you would have built if it had been there from the start, think that
  through, and deliver it in pieces.

#### Attack the Premise

- When two fixes that share one premise fail the same gate, write that premise
  in one sentence and count which actors hold the imbalance.
- Remove the assignment.
- Do not start another fix until you have done that.

#### Subtract Before You Add

- Take complexity out before you add anything.

#### Outcome-Oriented Execution

- Aim at the end state you can check.
- A break in the middle is allowed when you planned it and can undo it.

#### Build the Lever

- If the work is not obvious at a glance, build the tool that does it.
- Do one unit by hand, then make the tool match that unit.

#### Architecture

##### Model the Domain

- Put the domain in a structure that makes the illegal states impossible.
- Maintain a glossary of domain terms.
- If the code is already clear and local, leave it.

##### Boundary Discipline

- Check the data where it enters.
- Inside, trust it.
- Keep the business logic in a pure function.

##### Type System Discipline

- Make the illegal value impossible to write.
- Parse outside data once, where it comes in.
- Do not silence the compiler.

##### Make Operations Idempotent

- Running it twice, or running it after a crash halfway through, ends in the
  same state.

##### Separate Before Serializing Shared State

- If they do not need the same mutable thing, give each its own.
- If they do, allow one writer, and enforce that in the structure.

### Then Make it Right

#### Fix Root Causes

- Reproduce it.
- Fix the cause.
- A guard that only hides the crash is not a fix.

#### Sequence work into verifiable units

- Finish one small unit and check it.
- Then start the next.

#### Test Behavior, Not Implementation

- The test calls the code the way its user does and checks the result that user
  would see.
- If the test would still pass when every function returned nothing, throw it
  out.

#### Crapper

- Use the crapper tool.
- Reduce each function under CRAP 10 by splitting it, by testing it, or both.
- Consider whether case statements are worth reducing.

#### Dryer

- Use the dryer tool to eliminate duplication that rank 0.8 or above.

#### Mutator

- Use the mutator tool.
- Cover the uncovered.
- Kill the survivors.
- Delete dead code.

#### Refactoring

- Pin what the code does.
- Change the structure.
- Prove the behavior did not change.

### Practice

#### Keep a Log

- Keep a log of every action you take and the reason you took it.
- Store action logs under `TestArtifacts~/`.
- Report that log upon completion.

#### Guard the Context Window

- Keep the main thread small.
- Send the bulk elsewhere, and keep the summary.

#### Worktree Cleanup

- Find what is safe to remove, confirm nothing still in use needs it, then
  reclaim the disk.
- Uncommitted work waits for a decision.

#### Commit and Push

- Commit often, in small ordered commits.
- Each commit says why, what changed, and how you proved it.
- Push to the main line at the end.
- Include only changes belonging to the current task; preserve unrelated
  uncommitted work.

#### Tool Availability

- If crapper, dryer, or mutator is unavailable, report the limitation and any
  checks actually performed. Do not claim an unavailable tool ran or passed.
