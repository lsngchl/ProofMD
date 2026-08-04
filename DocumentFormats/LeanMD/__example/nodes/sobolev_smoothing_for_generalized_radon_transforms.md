# Sobolev Smoothing for the Generalized Radon Transform

Let \(n\ge1\) be an integer, let \(U,V\subset\mathbb R^n\) be open, and let \(\Phi\in C^\infty(U\times V;\mathbb R)\).
Let \(\chi\in C_c^\infty(U\times V)\).
Assume that \(\nabla_x\Phi\ne0\) and \(\nabla_y\Phi\ne0\) on \(U\times V\), and that, for every \(r\in\mathbb R\), the conormal canonical relation \(C_r\) of \(\Phi(x,y)=r\) is locally a canonical graph at every point arising from \((x,y)\in\operatorname{spt}\chi\).
For \(f\in C_c^\infty(V)\), define
\[
T_r^\chi f(x)
:=
\int_{\{y\in V:\Phi(x,y)=r\}}
f(y)\chi(x,y)\,d\sigma_{x,r}(y).
\]

## Proposition

For every \(s\in\mathbb R\), the operator \(T_r^\chi\) extends to a bounded map
\[
T_r^\chi:H^s(V)\longrightarrow H^{s+(n-1)/2}(U).
\]
More precisely, for every compact interval \(I\subset\mathbb R\), there is a constant \(C_{s,\chi,I}\) such that
\[
\|T_r^\chi f\|_{H^{s+(n-1)/2}(U)}
\le
C_{s,\chi,I}\|f\|_{H^s(V)},
\qquad r\in I.
\tag{SM}
\]
> **Remark.** Averaging \(f\) over the level hypersurfaces \(\Phi(x,y)=r\) improves \(L^2\)-Sobolev regularity by \((n-1)/2\).

## Proof

By the [coarea formula](./coarea_formula.md "why"), the [Schwartz kernel](./schwartz_kernel.md "why") of \(T_r^\chi\) is
\[
K_r(x,y)
=
\chi(x,y)|\nabla_y\Phi(x,y)|\,\delta\bigl(\Phi(x,y)-r\bigr).
\]
The Dirac delta distribution has the Fourier representation
\[
\delta(s)
=
\frac1{2\pi}
\int_{\mathbb R}e^{i\tau s}\,d\tau
\qquad\text{in }\mathcal S'(\mathbb R).
\]
The substitution \(s=\Phi(x,y)-r\) gives
\[
\delta\bigl(\Phi(x,y)-r\bigr)
=
\frac1{2\pi}
\int_{\mathbb R}e^{i\tau(\Phi(x,y)-r)}\,d\tau
\qquad\text{in }\mathcal D'(U\times V)
\]
([justification](./pullback_of_dirac_fourier_representation.md "why")).
The kernel therefore becomes
\[
K_r(x,y)
=
\frac1{2\pi}
\int_{\mathbb R}
e^{i\tau(\Phi(x,y)-r)}
\chi(x,y)|\nabla_y\Phi(x,y)|\,d\tau.
\]
Note that:

- The amplitude \(\chi(x,y)|\nabla_y\Phi(x,y)|\) is a symbol of order \(0\).
- The phase has the single frequency variable \(\tau\).
- \(\dim U=\dim V=n\).

Therefore, \(T_r^\chi\) is a [Fourier integral operator](./fourier_integral_operator.md "why") of order
\[
\mu
=
0+\frac12-\frac{n+n}{4}
=
-\frac{n-1}{2}.
\]
The phase \(\tau(\Phi-r)\) parametrizes \(C_r\), which is locally a canonical graph on \(\operatorname{spt}\chi\) by assumption, so the [Sobolev mapping property for Fourier integral operators](./sobolev_mapping_for_fourier_integral_operators.md "why") gives
\[
T_r^\chi:H^s(V)
\longrightarrow
H^{s-\mu}(U)
=
H^{s+(n-1)/2}(U).
\]
Because \(C_r\) is only locally a canonical graph, decompose \(\chi\) into finitely many pieces, each supported in a single canonical-graph patch.
Compactness of \(\operatorname{spt}\chi\) keeps the graph Jacobians uniformly away from \(0\) and uniformly controls the required phase and amplitude seminorms across these patches.
Since \(r\) only contributes \(-\tau r\) to the phase and \(T_r^\chi=0\) outside the compact set \(\Phi(\operatorname{spt}\chi)\), the patchwise constants are uniform in \(r\); summing the finitely many pieces gives \((\mathrm{SM})\). \(\square\)

## Application here

In the parent document,
\[
U,V\subset(\mathbb R^d)^m\cong\mathbb R^{md},
\]
so the proposition applies with
\[
n=md.
\]
The phase \(\Phi\), cutoff \(\chi\), and operator \(T_r^\chi\) there are exactly those in the proposition above.
Condition \((\mathrm{ND})\) gives \(\nabla_x\Phi\ne0\) and \(\nabla_y\Phi\ne0\), and the preceding projection argument shows that every \(C_r\) is locally a canonical graph on \(U\times V\), hence at every point arising from \(\operatorname{spt}\chi\).
Substituting \(n=md\) into the proposition gives
\[
\|T_r^\chi f\|_{H^{s+(md-1)/2}(U)}
\le
C_{s,\chi,I}\|f\|_{H^s(V)},
\qquad r\in I,
\]
for every compact interval \(I\subset\mathbb R\), which is precisely \((\mathrm{SM})\) in the parent document.
