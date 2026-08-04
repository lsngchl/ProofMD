# Why \(\Omega_m\) Meets \((\operatorname{spt}\mu)^{2m}\)

Let \(m\ge2\), let \(\mu\) be a compactly supported \(t\)-Frostman probability measure on \(\mathbb R^d\) with \(t>0\), and define
\[
\Omega_m
:=
\left\{
(x,y)\in(\mathbb R^d)^m\times(\mathbb R^d)^m:
\substack{x_i\ne y_i\ (1\le i\le m),\\
x_{i+1}\ne y_i\ (1\le i\le m-1)}
\right\}.
\]
Then
\[
\mu^{\otimes2m}(\Omega_m)=1,
\qquad
\Omega_m\cap(\operatorname{spt}\mu)^{2m}\ne\varnothing.
\]

## Proof

For every \(u\in\mathbb R^d\), the Frostman bound gives
\[
\mu(\{u\})\le\mu(B(u,r))\lesssim r^t\longrightarrow0
\]
as \(r\downarrow0\), so \(\mu\) is nonatomic.

For the diagonal \(\Delta:=\{(u,v)\in\mathbb R^d\times\mathbb R^d:u=v\}\), Fubini's theorem gives
\[
(\mu\otimes\mu)(\Delta)
=
\int_{\mathbb R^d}\mu(\{v\})\,d\mu(v)
=0.
\]
Therefore each collision set \(\{x_i=y_i\}\) and \(\{x_{i+1}=y_i\}\) has \(\mu^{\otimes2m}\)-measure zero.

Since \(\Omega_m^c\) is the finite union of these collision sets,
\[
\mu^{\otimes2m}(\Omega_m)=1.
\]
The product measure \(\mu^{\otimes2m}\) is supported on \((\operatorname{spt}\mu)^{2m}\), so this full-measure identity implies
\[
\Omega_m\cap(\operatorname{spt}\mu)^{2m}\ne\varnothing.
\]
