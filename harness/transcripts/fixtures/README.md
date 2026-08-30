# Fixture transcripts

**These are hand-authored fixtures, not recordings of real agent sessions.**

They exist so the harness loop can be demonstrated and tested with no agent CLI,
no network and no API key - and so the two cases worth showing are the same every
time rather than whatever a model happens to do on the day.

Say so when demonstrating them. A hand-written patch presented as a model's output
would be a lie, and an easy one to catch.

Real sessions are recorded by running the harness with `-record`:

```
harness -task tasks/undo-stack.json -agent "<your agent cli>" -record transcripts/undo-stack
```

Those recordings replace fixtures for demo purposes; these stay as test data.

| Fixture | Shows |
|---|---|
| `guard-demo/` | An agent doing real work *and* weakening a test. The harness refuses the run. |
| `self-heal/` | Iteration 1 introduces a regression the tests catch; iteration 2 fixes it. |

Regenerate them with `./make.sh` after changing the files they patch.
