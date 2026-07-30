# Schwartz Kernel

The Schwartz kernel is the distributional analogue of the kernel of an ordinary integral operator.
If \(U\subset\mathbb R^{n_1}\) and \(V\subset\mathbb R^{n_2}\) are open, the Schwartz kernel theorem says that every continuous linear operator
\[
T:C_c^\infty(V)\longrightarrow\mathcal D'(U)
\]
is represented by a unique distribution
\[
K\in\mathcal D'(U\times V).
\]
More precisely, for \(f\in C_c^\infty(V)\) and \(\psi\in C_c^\infty(U)\),
\[
\langle Tf,\psi\rangle
=
\langle K,\psi(x)f(y)\rangle.
\]
This distribution \(K\) is called the Schwartz kernel of \(T\).

When \(K\) is represented by a locally integrable function \(K(x,y)\), this statement reduces to the familiar formula
\[
Tf(x)=\int_V K(x,y)f(y)\,dy.
\]
This identity holds for almost every \(x\in U\).
Allowing \(K\) to be a distribution also covers operators concentrated on lower-dimensional sets.
For example, the identity operator has Schwartz kernel \(\delta(x-y)\), which is supported on the diagonal \(x=y\).
