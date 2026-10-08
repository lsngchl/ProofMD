# ProofFold format

A ProofFold project stores a proof as one Markdown entry document. Long
passages are moved into separate fold files, and ProofMD expands them in place
where they are linked. `01_project_name/` is a sample project.

This folder describes only the file structure that ProofMD reads. Rules for
writing ProofFold proofs are not part of the format and are kept with the
research projects that use it.

## Project layout

```
01_project_name/
├── prooffold.json   manifest
├── main.md          entry document
├── folds/           fold files, nested in subdirectories
├── notation.yaml    notation registry (not read by ProofMD)
└── references/      source audits (not read by ProofMD)
```

## Manifest

`prooffold.json` sits beside the entry document.

| Field | Required | Meaning |
| --- | --- | --- |
| `formatVersion` | yes | Must be `1`. |
| `entry` | yes | Entry Markdown file, relative to the manifest. |
| `foldsDirectory` | no | Directory holding the folds. Defaults to `folds`; it may be absent until the first fold exists. |
| `notationRegistry` | no | Location of the notation registry, for authors and tools. |
| `referencesDirectory` | no | Location of the source audits, for authors and tools. |

ProofMD enables ProofFold mode only when the opened file is the manifest's
`entry`. If the manifest is invalid or unreadable, the file opens as ordinary
Markdown.

## Fold links

A link whose title is `fold` (case-insensitive) is a fold link:

```markdown
[Fold: description](./folds/descriptive-name.md "fold")
[Fold (pending): description](./folds/descriptive-name.md "fold")
```

- The target is relative to the file containing the link and must lie inside
  the folds directory.
- ProofMD replaces the link with a collapsible section that shows the target
  in place. Fold links inside a fold expand recursively.
- Link text that starts with `Fold (pending):` marks an unfinished passage.
  ProofMD shows the section as pending.
- A missing target, a target outside the folds directory, or a link cycle is
  shown as an error, while the rest of the document stays readable.
- The map shows every fold reachable from the entry as a tree in link order.
