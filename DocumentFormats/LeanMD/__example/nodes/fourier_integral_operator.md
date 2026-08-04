# Fourier Integral Operators and Their Order

Let \(U\subset\mathbb R^{n_U}\) and \(V\subset\mathbb R^{n_V}\) be open.
An operator \(T:C_c^\infty(V)\to\mathcal D'(U)\) is a Fourier integral operator if its Schwartz kernel can be written locally as an oscillatory integral
\[
K(x,y)
=
\int_{\mathbb R^N}e^{i\varphi(x,y,\theta)}a(x,y,\theta)\,d\theta,
\]
where \(\varphi\) is a nondegenerate phase function and \(a\) is a symbol.

The amplitude \(a\) is a symbol of order \(q\) if, locally in \((x,y)\),
\[
\left|
\partial_x^\alpha
\partial_y^\beta
\partial_\theta^\gamma
a(x,y,\theta)
\right|
\le
C_{\alpha,\beta,\gamma}
(1+|\theta|)^{q-|\gamma|}.
\]
If the phase has \(N\) frequency variables and the amplitude is a symbol of order \(q\), then the Fourier integral operator has order
\[
\mu
=
q+\frac N2-\frac{n_U+n_V}{4}.
\]
Although \(q\) and \(N\) can change when the same kernel is represented by a different nondegenerate phase, the combination defining \(\mu\) is invariant.

In the parent document,
\[
\varphi(x,y,\tau)
=
\tau\bigl(\Phi(x,y)-r\bigr),
\qquad
a(x,y,\tau)
=
\frac1{2\pi}\chi(x,y)|\nabla_y\Phi(x,y)|.
\]
The amplitude is independent of \(\tau\), so it is a symbol of order \(q=0\).
There is one frequency variable, so \(N=1\), and \(n_U=n_V=n\).
Consequently,
\[
\mu
=
0+\frac12-\frac{n+n}{4}
=
-\frac{n-1}{2}.
\]
