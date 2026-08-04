# Coarea Formula

The coarea formula decomposes an integral over a Euclidean domain into integrals over the level sets of a function.
In its scalar form, let \(V\subset\mathbb R^n\) be open and let \(g:V\to\mathbb R\) be continuously differentiable.
For every nonnegative measurable function \(h\),
\[
\int_V h(y)|\nabla g(y)|\,dy
=
\int_{\mathbb R}
\left(
\int_{\{y\in V:g(y)=t\}}
h(y)\,d\sigma_t(y)
\right)dt,
\]
Here \(\sigma_t\) is the restriction of \((n-1)\)-dimensional Hausdorff measure to the regular part of \(g^{-1}(t)\):
\[
\sigma_t(E)
=
\mathcal H^{n-1}\bigl(E\cap g^{-1}(t)\bigr).
\]
Equivalently, if \(A\subset g^{-1}(t)\) is a compact smooth patch and
\[
\nu(y):=\frac{\nabla g(y)}{|\nabla g(y)|}
\]
is its unit normal, then
\[
\sigma_t(A)
=
\lim_{\varepsilon\downarrow0}
\frac{1}{2\varepsilon}
\left|
\{y+s\nu(y):y\in A,\ |s|<\varepsilon\}
\right|_n,
\]
where \(|\cdot|_n\) denotes \(n\)-dimensional Lebesgue measure and \(\varepsilon\) is small enough that the normal tube is embedded.

If \(\nabla g\ne0\) on \(g^{-1}(r)\), the same identity can be written using the Dirac delta distribution as
\[
\int_{\{g=r\}}h(y)\,d\sigma_r(y)
=
\int_V h(y)|\nabla g(y)|\,\delta(g(y)-r)\,dy.
\]
The expression \(\delta(g(y)-r)\) is rigorously defined as the [distributional pullback](./distributional_pullback.md "why") of the Dirac delta at \(r\) under \(g\).
Equivalently,
\[
\int_V h(y)\delta(g(y)-r)\,dy
=
\int_{\{g=r\}}
\frac{h(y)}{|\nabla g(y)|}\,d\sigma_r(y).
\]
The factor \(|\nabla g|\) compensates for how rapidly the value of \(g\) changes in the direction normal to its level sets.

## Application here

For fixed \(x\in U\), set
\[
g_x(y):=\Phi(x,y),
\qquad
h_x(y):=f(y)\chi(x,y).
\]
Then \(\nabla g_x(y)=\nabla_y\Phi(x,y)\), so the coarea identity gives
\[
\begin{aligned}
T_r^\chi f(x)
&=
\int_{\{y\in V:\Phi(x,y)=r\}}
f(y)\chi(x,y)\,d\sigma_{x,r}(y)\\
&=
\int_V
f(y)\chi(x,y)|\nabla_y\Phi(x,y)|
\delta\bigl(\Phi(x,y)-r\bigr)\,dy.
\end{aligned}
\]
Since \(T_r^\chi f(x)=\int_V K_r(x,y)f(y)\,dy\), its Schwartz kernel is
\[
K_r(x,y)
=
\chi(x,y)|\nabla_y\Phi(x,y)|\,\delta\bigl(\Phi(x,y)-r\bigr).
\]
