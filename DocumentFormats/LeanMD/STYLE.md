# LeanMD Writing Style

## 1. One node answers one question

Each node should immediately answer the question that led the reader to open it.

- A parent node states **what holds**.
- A `why` child explains **why it holds**.
- A concept node explains **what the concept means**.
- A source node gives **the exact cited statement and its source**.

Do not mix unrelated purposes in one node.

## 2. Do not hide the core argument behind child nodes

Keep short, direct calculations and connections essential to understanding the overall argument in the current node.

Move material to a child node only when it is an independently meaningful nontrivial fact, a long technical proof, a concept worth explaining separately, or a result imported from an external source.

Keep the main formula or conclusion in the parent node when moving its detailed derivation to a child node.

The depth of the logical document structure in the proof DAG should reflect the reader's natural questions, not merely the formal complexity of the proof.

## 3. Use only the necessary level of generality

A statement should not depend on notation accidental to one application, but it should not import generality that the argument does not use.

State results at the minimum useful level of generality.

When applying a general result, explicitly identify the parameters, objects, and hypotheses in the current setting.

## 4. Make each node self-contained at the required level

Restate any notation and hypotheses from a parent node that are needed to understand the child node.

Do not use phrases such as `as in the parent proposition` when the required setup can be stated directly and economically.

Place inherited setup in the introduction before the statement.

The statement should contain only the new assertion made by the node, not setup already fixed in its introduction.

Do not repeat parent material that is not needed for the child node.

## 5. State exact facts and ambient spaces

Make clear what the objects are, where a claim holds, which hypotheses are used, and what the conclusion is. Prefer direct formulas and specific names over phrases that require the reader to reconstruct the referent.

State the ambient space of every newly quantified variable when it is not already immediate from the local context.

State the domain and codomain of every newly introduced map.

When using a transpose or adjoint, specify the inner products, bilinear forms, or identifications that define it.

If an index labels edges, vertices, blocks, or another structural object rather than coordinates in the ambient vector space, make that role clear.

Use expressions such as `this condition`, `the source`, or `the relevant theorem` only when their referents are immediate and unambiguous.

## 6. Separate statement, proof, and interpretation

- A statement contains only what is asserted.
- A proof explains why the assertion holds.
- Interpretation and intuition belong in a `Remark`.
- The application of a general result may be placed in a separate application section when the correspondence is not immediate.

Place definitions and hypotheses where they are needed to understand the statement, but do not mix explanatory or proof material into the statement itself.

A displayed formula may stand alone as the statement when the heading and preceding setup already make its role clear.

Do not add an empty lead-in such as `The operators defined above satisfy` when the displayed assertion is already unambiguous.

## 7. Show the decisive intermediate calculation

When a conclusion depends on a contraction, block multiplication, kernel identity, restriction, transpose, or change of representation, display the intermediate equality that makes the conclusion immediate.

A phrase such as `by calculation`, `directly`, or `the formula above gives` is not a substitute for the calculation itself.

Show enough of the calculation to expose the mechanism, but do not expand routine algebra after the relevant mechanism is clear.

Use the same level of detail for parallel calculations.

If one of two analogous identities is expanded through a norm, inner product, or block component, expand the other identity to the corresponding level.

State whether a matrix, row vector, or covector is multiplied on the left or on the right when the distinction matters.

When deriving the coordinates of a transpose or adjoint, begin with its defining pairing identity and compare coefficients rather than simply announcing the resulting coordinate formula.

## 8. Prefer direct causal prose

State the hypothesis responsible for a conclusion directly.

Prefer sentences such as `Since \(w_0\in\ker H^T\),` over nominalized backward references such as `The kernel identity above gives`.

Use `Since`, `Because`, an explicit substitution, or a displayed calculation when these make the logical dependence immediate.

Do not describe a calculation with vague labels when the calculation itself can be written concisely.

Omit prose that merely repeats what an immediately following formula already states.

## 9. Introduce notation and names only when they are useful

Introduce a symbol or name only when it will actually be reused.

- Do not create abbreviations used only once.
- Do not abbreviate an expression unless the abbreviation materially shortens repeated formulas.
- Remove unused notation.
- Prefer established terminology when it exists.
- Do not present a project-local theorem name as standard terminology.
- Do not assign multiple roles to the same symbol.

Notation should reduce what the reader must process, not add another layer to decode.

Name a function before differentiating it when placeholder notation would obscure what is being differentiated.

For example, prefer defining \(h(z):=|z|\) and then writing \(D^2h(e_j)\) over writing \(D^2|\cdot|(e_j)\).

Prefer \(|e_j|\) itself over an abbreviation such as \(\ell_j:=|e_j|\) unless \(\ell_j\) is used often enough to simplify the argument materially.

## 10. Format calculations according to their logical structure

Use an `aligned` environment when a long calculation consists of several equality steps or when parallel calculations should appear on separate rows.

Place one parallel case on each row when combining them on one line would obscure their structure.

Line breaks in a displayed calculation should reflect logical stages, not merely source width.

Do not split an equality into more lines than are needed to reveal its mechanism.

## 11. Use links to express logical dependence

A `why` link is not merely a related-document link; it identifies the justification for the current claim.

- The linked phrase and the child node's statement should correspond directly.
- The reader should find the expected answer near the beginning of the child node.
- Keep the exact statement and citation of an external result in a source node.
- Do not duplicate the same source or justification through multiple link styles.

Choose the position of a `why` link according to the scope of what its target justifies.
Within a mathematical claim, attach a link directly to a word or phrase only when that linked expression is itself an established mathematical term, definition, or traditional theorem name.
Do not insert a project-local node title or filename into mathematical prose as though it were such an expression.
When a project-local node justifies the whole claim, state the claim directly and place the link to that node as a parenthetical reference at the end of the sentence.
When the target explains or supplies only a particular established term, traditional theorem, or mathematical ingredient within a sentence, attach the link directly to that word or phrase.

```md
By the [coarea formula](./coarea_formula.md "why"), the level-set integral can be written using a Dirac delta.
```

When a `why` link justifies an entire sentence, place it as a parenthetical reference at the end of that sentence, immediately before the final punctuation.
This placement keeps the link inside the sentence while making its scope the entire preceding claim.
If the sentence contains display mathematics, the display is part of the sentence, so place the parenthetical `why` link after the display and before the sentence's final punctuation.
Do not place such a link before the display it justifies or after the sentence-ending punctuation.

```md
The substitution gives
\[
\delta(h)
=
\frac{1}{2\pi}\int_{\mathbb R}e^{i\tau h}\,d\tau
\]
([Justification](./justification.md "why")).
```

Use the linked text to identify the destination node accurately.

When only one calculation or passage in the destination node is relevant, state outside the link what the reader should look for there.

For example:

```md
For the calculation of the coordinates of \(R_X^Tv\), see [Factorization of the Mixed Hessian on the Chain Plane](./mixed_hessian_factorization_on_chain_plane.md "why").
```

Do not make the link text look like the title of a dedicated subcalculation when the destination is actually a broader node.

## Summary

Each node should have one clear role, include the setup needed to understand it, state exact and properly typed facts at the minimum useful level of generality, and expose the decisive intermediate calculations.

Keep statements separate from inherited setup and proof, use notation only when it reduces cognitive load, and delegate only meaningful supporting details through precisely scoped `why` links.
