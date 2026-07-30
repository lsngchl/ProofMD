# Pullback of the Fourier Representation of the Dirac Delta

Let \(W\subset\mathbb R^N\) be open, and let \(h:W\to\mathbb R\) be a smooth submersion.
For \(R>0\), define
\[
\delta_R(s)
:=
\frac1{2\pi}
\int_{-R}^{R}e^{i\tau s}\,d\tau.
\]
For every \(\varphi\in\mathcal D(\mathbb R)\),
\[
\left\langle\delta_R,\varphi\right\rangle
=
\frac1{2\pi}
\int_{-R}^{R}\widehat\varphi(-\tau)\,d\tau
\longrightarrow
\varphi(0)
=
\left\langle\delta_0,\varphi\right\rangle.
\]
Thus \(\delta_R\to\delta_0\) in the weak-* topology of \(\mathcal D'(\mathbb R)\).

The pullback \(h^*:\mathcal D'(\mathbb R)\to\mathcal D'(W)\) is weak-* continuous.
Indeed, for every \(\psi\in\mathcal D(W)\),
\[
\begin{aligned}
\left\langle h^*\delta_R,\psi\right\rangle
&=
\left\langle\delta_R,h_*\psi\right\rangle\\
&\longrightarrow
\left\langle\delta_0,h_*\psi\right\rangle\\
&=
\left\langle h^*\delta_0,\psi\right\rangle,
\end{aligned}
\]
because \(h_*\psi\in\mathcal D(\mathbb R)\).
For finite \(R\), the function \(\delta_R\) is smooth, so
\[
h^*\delta_R(z)
=
\delta_R(h(z))
=
\frac1{2\pi}
\int_{-R}^{R}e^{i\tau h(z)}\,d\tau.
\]
Passing to the distributional limit gives
\[
h^*\delta_0(z)
=
\frac1{2\pi}
\int_{\mathbb R}e^{i\tau h(z)}\,d\tau
\qquad\text{in }\mathcal D'(W).
\]

In the parent document, \(W=U\times V\) and \(h(x,y)=\Phi(x,y)-r\).
The assumption \(\nabla_y\Phi\ne0\) makes \(h\) a submersion, so the preceding argument yields
\[
\delta\bigl(\Phi(x,y)-r\bigr)
=
\frac1{2\pi}
\int_{\mathbb R}e^{i\tau(\Phi(x,y)-r)}\,d\tau
\qquad\text{in }\mathcal D'(U\times V).
\]
