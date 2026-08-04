# Pullback of a Distribution by a Submersion

Let \(U\subset\mathbb R^m\) and \(V\subset\mathbb R^n\) be open, let \(F:U\to V\) be a smooth submersion, and let \(u\in\mathcal D'(V)\).
Because \(F\) is a submersion, pushforward defines a continuous linear map
\[
F_*:\mathcal D(U)\to\mathcal D(V).
\]
Consequently, the pullback of \(u\) is well-defined by
\[
\langle F^*u,\omega\rangle
:=
\langle u,F_*\omega\rangle,
\qquad \omega\in\mathcal D(U).
\]
That is, \(F^*:\mathcal D'(V)\to\mathcal D'(U)\) is the dual map of \(F_*:\mathcal D(U)\to\mathcal D(V)\).

Now let \(g:V\subset\mathbb R^n\to\mathbb R\) be smooth, and suppose that
\[
\nabla g(y)\ne0,
\qquad y\in g^{-1}(r).
\]
Then \(g\) is a submersion near \(g^{-1}(r)\), so the Dirac delta \(\delta_r\) can be pulled back by \(g\).
This pullback is denoted by
\[
g^*\delta_r
=
\delta(g-r).
\]
For \(\varphi\in\mathcal D(V)\), the pushforward \(g_*\varphi\) is smooth near \(r\), with
\[
(g_*\varphi)(r)
=
\int_{\{g=r\}}
\frac{\varphi(y)}{|\nabla g(y)|}\,d\sigma_r(y).
\]
Therefore, the definition of pullback gives
\[
\begin{aligned}
\left\langle\delta(g-r),\varphi\right\rangle
&=
\left\langle g^*\delta_r,\varphi\right\rangle\\
&=
\left\langle\delta_r,g_*\varphi\right\rangle\\
&=
(g_*\varphi)(r)\\
&=
\int_{\{g=r\}}
\frac{\varphi(y)}{|\nabla g(y)|}\,d\sigma_r(y).
\end{aligned}
\]
Hence \(\delta(g-r)\) is the distribution supported on \(g^{-1}(r)\) whose density relative to the induced hypersurface measure is \(1/|\nabla g|\).
