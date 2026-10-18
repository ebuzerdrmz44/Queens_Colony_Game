# Isolated flow regression checks

Run from the project root:

```bash
bash Tests/FlowRegression/run.sh
```

Set `UNITY_EDITOR_DIR` to use another editor installation. The script compiles the actual
`Tutorial.cs` and `SaveData.cs` sources plus the current `ColonyGame.StartNewGame` method and wave-report accounting methods against small collaborators and runs 53 behavior checks.
It uses a unique temporary save directory and deletes it afterwards; player saves are untouched.

Coverage: explicit tutorial progression, delayed dismissal, real outcome signals, pause,
review/skip, independent-step skipping and required prerequisites, skipped-view preparation, explicit-only tutorial startup, unfinished and completed goal restoration, older saves, preparation time,
first/repeated atomic writes, backup contents, corrupt JSON, and preserving the old save
when writing fails.

These are isolated tests. `RuntimeStubs.cs` substitutes Unity services and uses a managed JSON
serializer. They do not verify Unity's native JSON behavior, rendering, hover appearance,
scene simulation, or input coordinates. Runtime compilation against real Unity assemblies is
checked separately. Unity Play mode should be used for visual interaction verification.

Wave checks use methods extracted from the current WaveSpawner source: kill attribution during
overlapping waves, duplicate removal, pending-spawn completion, remaining counts, casualties by
attacker wave, separate starvation accounting, partial legacy history, and disk persistence.
They do not simulate complete Unity coroutines or actual combat.
