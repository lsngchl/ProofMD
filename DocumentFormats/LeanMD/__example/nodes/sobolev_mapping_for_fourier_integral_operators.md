# Sobolev Mapping Property for Fourier Integral Operators

## Statement

Let \(U,V\subset\mathbb R^n\) be open, and let \(T\) be a compactly supported Fourier integral operator of order \(\mu\) whose canonical relation is locally a canonical graph.
Then, for every \(s\in\mathbb R\),
\[
T:H^s(V)\longrightarrow H^{s-\mu}(U)
\]
is bounded.

## Proof

Let \(\Lambda_U=(1-\Delta_U)^{1/2}\) and \(\Lambda_V=(1-\Delta_V)^{1/2}\), which are pseudodifferential operators of order \(1\) defining the Sobolev norms on \(U\) and \(V\).
Set
\[
A
:=
\Lambda_U^{s-\mu}T\Lambda_V^{-s}.
\]
Composition with the two pseudodifferential operators changes the order by
\[
(s-\mu)+\mu-s=0,
\]
so \(A\) is a Fourier integral operator of order \(0\).
The canonical relations of \(\Lambda_U^{s-\mu}\) and \(\Lambda_V^{-s}\) are the diagonal relations \(\Delta_U\) and \(\Delta_V\), respectively.
Here
\[
\Delta_U
:=
\left\{
((x,\xi),(x,\xi)):
x\in U,\ \xi\in\mathbb R^n\setminus\{0\}
\right\},
\]
and
\[
\Delta_V
:=
\left\{
((y,\eta),(y,\eta)):
y\in V,\ \eta\in\mathbb R^n\setminus\{0\}
\right\}.
\]
Hence the canonical relation of \(A\) is
\[
\Delta_U\circ C\circ\Delta_V
=
C,
\]
where \(C\) is the canonical relation of \(T\).

The adjoint \(A^*\) has order \(0\) and canonical relation \(C^{-1}\).
Because \(C\) is locally a graph,
\[
C^{-1}\circ C
=
\Delta_V,
\]
so the Fourier integral operator composition calculus shows that \(A^*A\) is a pseudodifferential operator whose symbol has order \(0\).

The Calderón–Vaillancourt theorem states that every pseudodifferential operator whose symbol has order \(0\) is bounded on \(L^2\).
See Elias M. Stein, [*Harmonic Analysis: Real-Variable Methods, Orthogonality, and Oscillatory Integrals*](https://doi.org/10.1515/9781400883929), Chapter VI, Section 2.
Applying the theorem to \(A^*A\) gives
\[
\|A^*Ag\|_{L^2(V)}
\lesssim
\|g\|_{L^2(V)}.
\]
Consequently,
\[
\begin{aligned}
\|Ag\|_{L^2(U)}^2
&=
\langle A^*Ag,g\rangle_{L^2(V)}\\
&\le
\|A^*Ag\|_{L^2(V)}\|g\|_{L^2(V)}\\
&\lesssim
\|g\|_{L^2(V)}^2.
\end{aligned}
\]
Thus \(A:L^2(V)\to L^2(U)\) is bounded.

Taking \(g=\Lambda_V^s f\) yields
\[
\begin{aligned}
\|Tf\|_{H^{s-\mu}(U)}
&=
\|\Lambda_U^{s-\mu}Tf\|_{L^2(U)}\\
&=
\|Ag\|_{L^2(U)}\\
&\lesssim
\|g\|_{L^2(V)}\\
&=
\|f\|_{H^s(V)},
\end{aligned}
\]
which proves the stated mapping property.
