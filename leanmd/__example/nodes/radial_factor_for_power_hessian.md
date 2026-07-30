# The Radial Factor \(g^TA^{-1}g\)

Let \(a>0\) with \(a\ne1\), let \(v\in\mathbb R^d\setminus\{0\}\), and set
\[
h_a(z):=|z|^a,
\qquad
g:=Dh_a(v)=a|v|^{a-2}v,
\qquad
A:=D^2h_a(v).
\]
Then
\[
A^{-1}g=\frac{v}{a-1},
\qquad
g^TA^{-1}g=\frac{a}{a-1}|v|^a.
\]

## Proof

The Hessian formula gives
\[
A
=a|v|^{a-2}I_d+a(a-2)|v|^{a-4}vv^T,
\]
and hence
\[
Av=a(a-1)|v|^{a-2}v.
\]
Therefore
\[
\begin{aligned}
A^{-1}g
&=a|v|^{a-2}A^{-1}v\\
&=\frac{a|v|^{a-2}}{a(a-1)|v|^{a-2}}v\\
&=\frac{v}{a-1}.
\end{aligned}
\]
It follows that
\[
\begin{aligned}
g^TA^{-1}g
&=\bigl(a|v|^{a-2}v\bigr)^T\frac{v}{a-1}\\
&=\frac{a}{a-1}|v|^a.
\end{aligned}
\]
