# ProofFold authoring rules

## Core reading contract

Treat `main.md` as the canonical proof.  Write the argument there in its
natural reading order from the statement to the final conclusion.

A fold is a passage removed from that linear proof only to keep a lengthy
calculation, hypothesis check, or technical reduction from obscuring the main
line.  It is not an independent note.  It inherits every definition,
hypothesis, convention, and piece of notation available at its insertion
point.

Every fold boundary must support both readings:

1. With the fold collapsed, `main.md` must state clearly what the fold proves,
   and the text after the link may use exactly that result.
2. With the fold expanded in place, the text before the link, the complete
   fold, and the text after the link must read as one continuous argument.

The same rule applies recursively to a fold nested inside another fold.  Do
not make a fold repeat the document setup merely so that it can be read alone.
End it at the point where the parent document has obtained the result needed
to continue.

### Fold-blind proof prose

Write every passage in `main.md` and `folds/**/*.md` as though every fold link
had been recursively replaced by its contents and the result were one
continuous `main.md`.  A proof sentence must not depend on the reader knowing
that a fold boundary or file boundary exists.

Except for the `[Fold: ...]` link itself, proof prose must not refer to folds,
files, parent or child documents, or collapsed or expanded state.  In
particular, do not write “this fold,” “the preceding fold,” “the following
fold,” “the linked fold,” “the dedicated fold,” or analogous expressions.
Refer instead to mathematical content: a named calculation, hypothesis check,
result, or equation number.  The word *fold* remains available for mathematical
objects such as Whitney folds, fold atoms, fold phases, and fold geometry.

When a passage is moved into or out of a fold, revise proximity language and
equation references so that they remain correct in the recursively expanded
linear proof.  Equation labels must occur in that natural reading order;
claims may be announced in prose before a fold link and displayed with their
labels after the folded derivation.

## File roles

- `main.md` contains the theorem and the complete visible proof line.
- `folds/` contains only passages folded at specific insertion points.
- `notation.yaml` records notation with a persistent document-wide meaning.
- `references/` contains one source audit for each specialized external work.
- `prooffold.json` records the machine-readable locations of those components.

Do not create README files, a separate document index, progress summaries, or
status ledgers.  Authoring instructions belong in this file.  The exact fold
link is the only place where unfinished proof status is recorded.

## Creating and completing folds

First write or outline the proof in `main.md`.  Move a passage into `folds/`
only when its role and its required conclusion are clear.  Give the file a
descriptive lowercase hyphenated name and insert it at the exact point where
its contents belong:

```markdown
[Fold: description of the omitted passage](./folds/descriptive-name.md "fold")
```

For a nested fold, use the corresponding relative path from its parent file.
Mirror the fold-containment graph in the directory tree, but keep that physical
tree shallow enough to remain portable.  Here directory depth means the number
of directories strictly between `folds/` and the fold file: `folds/foo.md` has
depth zero and `folds/foo/bar.md` has depth one.

- Files linked directly from the project `main.md` live immediately under
  `folds/` at depth zero.
- At directory depths one and two, if `foo.md` contains a direct child fold,
  store that child under the sibling directory `foo/`, using the full parent
  stem.
- At directory depths three and four, continue to mirror the parent--child
  relation but use a unique, readable contraction of the parent stem for the
  new directory.  Such a directory name must be at most 20 characters and
  must not be an opaque serial, hash, or generic label such as `calc1`.
- Do not create a fold below directory depth four.  A fold at depth four may
  not contain another fold link.  If the proof appears to require a deeper
  fold, merge that calculation into its parent or reorganize an earlier proof
  division; never omit the required argument.

Every fold filename, including the `.md` extension, must be at most 48
characters.  Every repository-root-relative fold path must be at most 180
characters, and its absolute path in the active checkout must be at most 220
characters.  Shorten descriptive stems or reorganize the fold hierarchy before
crossing any of these limits; do not rely on platform-specific long-path
settings.

The directory relation records only where a passage is inserted.  It does not
turn a nested fold into an independent document or alter the inherited reading
context.

If a required passage has not yet been written, create its target file with
only an invisible placeholder comment and label the link:

```markdown
[Fold (pending): description of the missing passage](./folds/descriptive-name.md "fold")
```

Do not describe the same gap elsewhere.  Once the argument is written, remove
`pending` from the link.  A completed fold must establish precisely the result
that the collapsed parent text assumes.

## Notation registry

Introduce symbols naturally where the proof first needs them.  Whenever a
symbol acquires a persistent document-wide meaning, add one entry to
`notation.yaml` with `symbol`, `name`, `meaning`, and `introduced_at`.

Record only genuinely persistent notation.  Do not register bound variables,
dummy indices, temporary scale parameters, or standard mathematical syntax.
The registry contains only global notation, so do not add a `scope` field.
Before introducing a persistent symbol, check the entire registry for both
literal and visual conflicts.

The registry also records document-wide notation conventions.  Reserve a
local-looking symbol there when it keeps one semantic role across several
stages of the expanded proof, and record general naming rules separately from
named mathematical objects.  Do not use this mechanism to register disposable
dummy variables.

Local notation is scoped by the fully expanded linear proof, not by file
boundaries.  A fold and every nested fold inherit the local bindings active at
their insertion point and must not reassign them.  When a long passage uses
several simultaneous scales or indices, introduce their roles together before
the decomposition.  If a local role survives beyond that passage or recurs in
several proof stages, promote it to a document-wide convention.

## Reference audits

For every specialized result imported from the literature, create one audit
under `references/` for the cited work and link it at the point of use.  Each
audit must record:

- the exact edition or version and stable citation;
- the inspected theorem, definition, equation, and page locations;
- what the source actually proves;
- where and how ProofFold uses it;
- every source hypothesis and its project-local verification;
- any additional rescaling, normalization, compactness, or continuity bridge;
- a direct verdict stating whether the imported form is justified.

Treat secondary notes and downloaded derivations as leads, not as authority.
Check specialized claims against the primary source.  In particular,
distinguish identities valid only on the incidence set from identities needed
on an amplitude neighborhood, and recompute any off-incidence extension used
to verify a source class.

## Completion checks

Before declaring the proof complete, verify all of the following:

- `main.md` is readable with every fold collapsed;
- recursively expanding every fold gives a continuous proof;
- after recursive expansion, no proof sentence depends on knowing a fold or
  file boundary, and no organizational fold/file language remains outside the
  fold links themselves;
- no `Fold (pending)` link or placeholder remains;
- every fold is linked from its actual insertion point and no fold is orphaned;
- the fold directory tree matches the parent--child links and satisfies the
  depth, segment, filename, repository-relative-path, and absolute-path limits
  stated above;
- every imported specialized result has a linked source audit;
- all relative links and all paths in `prooffold.json` resolve;
- `notation.yaml` parses, has no duplicate symbol, and every `introduced_at`
  path exists;
- equation labels are unique and occur in natural reading order in the fully
  expanded document;
- Markdown tables, code fences, and mathematics delimiters render correctly;
- exact formulas used off incidence have been checked with the definitions
  actually imposed by the cited source, not merely with an incidence-equivalent
  formula.

Keep an unfinished marker only at the exact missing fold.  Do not dilute proof
status by scattering caveats through `main.md` or unrelated files.
