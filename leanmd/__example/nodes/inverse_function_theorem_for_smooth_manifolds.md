# The Inverse Function Theorem for Smooth Manifolds

## The theorem

Let \(M\) and \(N\) be smooth manifolds of the same dimension, let \(F:M\to N\) be smooth, and let \(p\in M\).
The differential of \(F\) at \(p\) is the linear map
\[
dF_p:T_pM\longrightarrow T_{F(p)}N.
\]
If \(dF_p\) is invertible, then there are open neighborhoods \(M_0\subset M\) of \(p\) and \(N_0\subset N\) of \(F(p)\) such that
\[
F|_{M_0}:M_0\longrightarrow N_0
\]
is a diffeomorphism.
Thus \(F\) has a smooth local inverse near \(p\), and one says that \(F\) is a local diffeomorphism at \(p\).

Because the tangent spaces have the same finite dimension, it is enough to prove
\[
\ker dF_p=\{0\}.
\]
Indeed, an injective linear map between vector spaces of the same finite dimension is invertible.
In local coordinate charts, this statement is exactly the ordinary inverse function theorem for maps between Euclidean spaces.

## Application to \(C_r\)

Via the parametrization by \((x,y,\tau)\), the canonical relation \(C_r\) is identified with
\[
\left\{
(x,y,\tau)\in U\times V\times(\mathbb R\setminus\{0\}):
\Phi(x,y)=r
\right\}.
\]
Condition \((\mathrm{ND})\) implies \(d\Phi\ne0\), so this is a smooth manifold of dimension
\[
(2md+1)-1=2md.
\]
The cotangent bundle \(T^*V\) also has dimension \(2md\).
The calculation in the parent document proves
\[
\ker d\pi_R=\{0\}.
\]
Consequently,
\[
d\pi_R:T_{(x,y,\tau)}C_r
\longrightarrow
T_{\pi_R(x,y,\tau)}(T^*V)
\]
is invertible, and the inverse function theorem makes \(\pi_R\) a local diffeomorphism.
The same argument, with \(x\) and \(y\) interchanged, gives the same conclusion for \(\pi_L:C_r\to T^*U\).
