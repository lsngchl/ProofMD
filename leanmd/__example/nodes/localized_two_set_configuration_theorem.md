# Nonempty Interior of the Configuration Image

## Theorem

Let \(m\ge2\) and let \(d\ge1\) be an integer.
Let \(U,V\subset(\mathbb R^d)^m\) be open, let \(A\subset U\) and \(B\subset V\) be compact, and let
\[
\Phi\in C^\infty(U\times V;\mathbb R).
\]
Assume that
\[
\det
\begin{pmatrix}
0 & \nabla_x\Phi(x,y)^T\\
\nabla_y\Phi(x,y) & \Phi_{yx}(x,y)
\end{pmatrix}
\ne0,\qquad (x,y)\in U\times V,
\tag{ND}
\]
and that
\[
\dim_HA+\dim_HB>md+1.
\tag{DIM}
\]
Then
\[
\operatorname{int}\Phi(A,B)\ne\varnothing.
\]

## Proof

Condition \((\mathrm{ND})\) first implies
\[
\nabla_x\Phi(x,y)\ne0,
\qquad
\nabla_y\Phi(x,y)\ne0,
\qquad (x,y)\in U\times V.
\]
Indeed, if either gradient vanished, the first row or the first column of the matrix in \((\mathrm{ND})\) would vanish.

For each \(r\in\mathbb R\), the [conormal canonical relation](./conormal_canonical_relation.md "why") of the incidence hypersurface \(\{(x,y):\Phi(x,y)=r\}\) is
\[
\begin{aligned}
C_r
=
\Bigl\{
&\Bigl(\bigl(x,\tau\nabla_x\Phi(x,y)\bigr),\bigl(y,-\tau\nabla_y\Phi(x,y)\bigr)\Bigr):\\
&\Phi(x,y)=r,\quad (x,y)\in U\times V,\quad \tau\ne0
\Bigr\}.
\end{aligned}
\]
Using the parametrization of \(C_r\) by triples \((x,y,\tau)\) satisfying \(\Phi(x,y)=r\) and \(\tau\ne0\), we verify that the natural projections
\[
\begin{aligned}
\pi_L(x,y,\tau)&:=\bigl(x,\tau\nabla_x\Phi(x,y)\bigr),\\
\pi_R(x,y,\tau)&:=\bigl(y,-\tau\nabla_y\Phi(x,y)\bigr)
\end{aligned}
\]
are local diffeomorphisms.
Suppose
\[
(\dot x,\dot y,\dot\tau)\in\ker d\pi_R,
\]
and choose a smooth curve \((x(s),y(s),\tau(s))\in C_r\) through \((x,y,\tau)\) whose derivative at \(s=0\) is \((\dot x,\dot y,\dot\tau)\).
Differentiating \(\pi_R(x(s),y(s),\tau(s))\) at \(s=0\) gives
\[
d\pi_R(\dot x,\dot y,\dot\tau)
=
\left(
\dot y,
-\dot\tau\nabla_y\Phi
-\tau\Phi_{yx}\dot x
-\tau\Phi_{yy}\dot y
\right)
=0,
\]
where the derivatives of \(\Phi\) are evaluated at \((x,y)\).
The first component gives
\[
\dot y=0.
\]
Since the curve lies in \(C_r\), it satisfies
\[
\Phi(x(s),y(s))=r.
\]
Differentiating this identity at \(s=0\) gives
\[
\nabla_x\Phi(x,y)^T\dot x
+
\nabla_y\Phi(x,y)^T\dot y
=0.
\]
Using \(\dot y=0\), we obtain
\[
\nabla_x\Phi(x,y)^T\dot x=0.
\]
The second component of \(d\pi_R(\dot x,\dot y,\dot\tau)=0\), together with \(\dot y=0\) and \(\tau\ne0\), gives
\[
\Phi_{yx}(x,y)\dot x
+\frac{\dot\tau}{\tau}\nabla_y\Phi(x,y)=0.
\]
Therefore
\[
\begin{pmatrix}
0 & \nabla_x\Phi(x,y)^T\\
\nabla_y\Phi(x,y) & \Phi_{yx}(x,y)
\end{pmatrix}
\binom{\dot\tau/\tau}{\dot x}
=0.
\]
Condition \((\mathrm{ND})\) gives \(\dot x=0\) and \(\dot\tau=0\).
Thus
\[
\ker d\pi_R=\{0\}.
\]
Since
\[
\dim C_r=\dim T^*V=2md,
\]
the differential \(d\pi_R\) is invertible, and the [inverse function theorem](./inverse_function_theorem_for_smooth_manifolds.md "why") shows that \(\pi_R\) is a local diffeomorphism.
The same argument, with \(x\) and \(y\) interchanged, shows that \(d\pi_L\) is also invertible, so \(\pi_L\) is a local diffeomorphism as well.
Thus \(C_r\) is locally a canonical graph.

Choose \(\chi\in C_c^\infty(U\times V)\) such that \(\chi=1\) on a neighborhood of \(A\times B\).
For \(r\in\mathbb R\), define the localized generalized Radon transform
\[
T_r^\chi f(x)
:=
\int_{\{y\in V:\Phi(x,y)=r\}}
f(y)\chi(x,y)\,d\sigma_{x,r}(y),
\]
where \(d\sigma_{x,r}\) is the induced hypersurface measure.
The local canonical graph property gives, for every \(s\in\mathbb R\),
\[
\|T_r^\chi f\|_{H^{s+(md-1)/2}(U)}
\lesssim
\|f\|_{H^s(V)},
\tag{SM}
\]
locally uniformly in \(r\) ([Sobolev Smoothing for the Generalized Radon Transform](./sobolev_smoothing_for_generalized_radon_transforms.md "why")).
Conditions \((\mathrm{DIM})\) and \((\mathrm{SM})\) give
\[
\operatorname{int}\Phi(A,B)\ne\varnothing
\]
([The Two-Set Configuration Theorem](./two_set_configuration_theorem.md "why")).
\(\square\)

> **Remark.** Condition \((\mathrm{ND})\) makes the two projections of \(C_r\) local diffeomorphisms, so \(C_r\) is locally a canonical graph and the Sobolev estimate \((\mathrm{SM})\) follows.
> Condition \((\mathrm{DIM})\) is a separate dimension hypothesis on \(A\) and \(B\); combining it with \((\mathrm{SM})\) gives the nonempty-interior conclusion.
