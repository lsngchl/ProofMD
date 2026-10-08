# ProofFold authoring rules

## Project directories

- Name project directories `NN_project_name`, for example `01_project_name`.
- Name subproject directories `NN-sMM_subproject_name`, for example
  `01-s01_subproject_name`. `NN` is the parent project's number, and `MM` is
  the subproject's number within that project.
- For new projects and subprojects, use two-digit numbers starting at `01`.
  Use lowercase names with words separated by underscores.
- When normalizing existing directories, preserve established identifiers
  such as `00`, `02a`, and `02b`, and keep archived projects in their archive.
- Store new project and subproject directories directly under `ProofFold/`.

## Proof and folds

Write all ProofFold documents, including source audits and `notation.yaml`,
in English.

`main.md` is the canonical proof, ordered from the statement to the conclusion.
A fold holds a lengthy calculation, hypothesis check, or technical reduction
at its exact insertion point. It inherits all definitions, hypotheses,
conventions, and local notation active there; do not repeat setup for standalone
reading. End the passage when the parent has obtained the result it needs.

At every nesting level, the collapsed parent must state the result supplied by
the fold and use exactly that result. State it as a precise claim, including
its quantifiers and what any implicit constants depend on. Recursive expansion
must yield one continuous proof. Outside fold links, proof prose must not refer
to file organization. After moving passages, fix proximity language and
equation references; labels must follow the expanded reading order.

Source material, including AI-generated drafts, is a lead, not a limit. Where
a step is missing, wrong, or only sketched, work out a correct argument
yourself. Write only steps you have checked, and mark a step `pending` only
when a serious attempt fails to establish it. If a correct argument needs an
extra hypothesis or gives a weaker conclusion, change the statement explicitly
and report the change to the user. A step justified only by words such as
"clearly", "standard", "similarly", or "it is easy to see" is a gap: give the
argument, cite a source audit, or point to where the expanded proof
establishes it.

First write or outline `main.md`; fold a passage only once its role and required
conclusion are clear. Use descriptive lowercase hyphenated filenames and links
relative to the containing file:

```markdown
[Fold: description](./folds/descriptive-name.md "fold")
[Fold (pending): description](./folds/descriptive-name.md "fold")
```

For an unfinished passage or step, create its target and mark only the
insertion link as `pending`. The target may retain established partial
arguments and identify the remaining step; use an invisible placeholder comment
if unwritten. Remove `pending` once the argument establishes the full result
required by the parent.

Record component locations in `prooffold.json`. Do not add README files,
separate indexes, progress summaries, or status ledgers. Keep authoring
instructions here.

## Fold paths

Mirror fold containment in the directory tree. Depth counts directories between
`folds/` and the file, so `folds/foo.md` has depth zero.

- Folds inserted directly into `main.md` live at depth zero.
- At every depth, store children of `foo.md` in its sibling directory `foo/`,
  using the full parent stem.
- Filename stems, including hyphens, must be at most 16 characters; aim for
  10–14. Use the surrounding path as context and put fuller descriptions in
  fold link text.
- Maximum depth: four. A fold at depth four cannot contain a fold link.
- Maximum path lengths: repository-relative, 180 characters; absolute checkout,
  220 characters.

Shorten names, merge passages, or reorganize the proof to meet these limits
without omitting arguments or relying on platform-specific long-path settings.

## Notation

Introduce symbols at first use. Before assigning a non-dummy symbol, consult
`notation.yaml` and search for the symbol's earlier uses in the expanded proof.
Preserve active local bindings across fold boundaries. Reuse a symbol for
another role only after its earlier role has clearly ended and cannot
reasonably carry forward.
Distinguish simultaneous scales and indices, introducing their roles together
before the decomposition.

Register persistent document-wide notation with `symbol`, `name`, `meaning`,
and `introduced_at`. Exclude bound variables, dummy indices, temporary scales,
and standard syntax; do not add `scope`. Reserve recurring semantic roles in
`conventions.reserved_symbols`, including local-looking symbols used across
proof stages. Keep general naming rules in `conventions.naming_rules`.

After substantial additions or reorganization, review repeated non-dummy
symbols throughout the expanded proof, including unregistered ones, and remove
confusing changes of meaning.

## Source audits

Audit specialized results imported from the literature in `references/` and
link the audits at the point of use. An audit may cover multiple works;
distinguish each work's citation, inspected locations, and actual contribution.
Check primary sources; treat secondary notes and downloaded derivations as
leads. Record:

- Exact edition/version, stable citation, and inspected theorem, definition,
  equation, and page locations.
- What the source actually proves, including its hypotheses and scope.
- Where it is cited and whether the source supports the attributed result.

Write all project-local reasoning once in the proof flow of `main.md` or
`folds/**/*.md`, including why each source hypothesis holds and any rescaling,
normalization, compactness, or continuity bridge. Source audits may link to
these passages; do not move or duplicate them into audits, or replace them in
the proof with audit links.

## Completion

`_tools/check_prooffold.py` in the ResearchDB folder that contains this
research folder checks pending links, placeholders, orphan folds, fold paths,
links, `prooffold.json`, `notation.yaml`, equation labels, and math
delimiters. During writing, run it with `--allow-pending` on the project you
are changing:

```sh
python <ResearchDB>/_tools/check_prooffold.py --allow-pending ProofFold/<project>
```

Before declaring completion, run it without `--allow-pending` and fix every
error. Then review the collapsed and fully expanded proof against these rules,
and check that Markdown tables render correctly.
