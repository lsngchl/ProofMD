# Nondegeneracy for \(a=1\)

Let \(d\ge2\) and \(m\ge2\).
For \(x=(x_1,\ldots,x_m),y=(y_1,\ldots,y_m)\in(\mathbb R^d)^m\), define
\[
\Phi_{1,m}(x,y)
=
\sum_{i=1}^m|x_i-y_i|
+
\sum_{i=1}^{m-1}|x_{i+1}-y_i|.
\]

## Proposition

Let
\[
z_1^0,z_2^0,\ldots,z_{2m}^0\in\mathbb R^d
\]
satisfy the following conditions:

1. every edge \(z_{j+1}^0-z_j^0\) is nonzero;
2. all vertices lie in an affine two-plane;
3. the directions of \(z_j^0-z_{j-1}^0\) and \(z_{j+1}^0-z_j^0\) are not parallel for \(2\le j\le2m-1\).

Define
\[
x^0:=(z_1^0,z_3^0,\ldots,z_{2m-1}^0),
\qquad
y^0:=(z_2^0,z_4^0,\ldots,z_{2m}^0).
\]
Then
\[
\det
\begin{pmatrix}
0 & \nabla_x\Phi_{1,m}(x^0,y^0)^T\\
\nabla_y\Phi_{1,m}(x^0,y^0) & (\Phi_{1,m})_{yx}(x^0,y^0)
\end{pmatrix}
\ne0.
\]

## Proof

For \(1\le j\le2m-1\), put
\[
e_j:=z_{j+1}^0-z_j^0,
\qquad
u_j:=\frac{e_j}{|e_j|}.
\]
Set
\[
H:=(\Phi_{1,m})_{yx}(x^0,y^0),
\qquad
v_0:=(u_1,0,\ldots,0),
\qquad
w_0:=(0,\ldots,0,u_{2m-1}).
\]
The mixed Hessian satisfies
\[
\ker H=\operatorname{span}\{v_0\},
\qquad
\ker H^T=\operatorname{span}\{w_0\}
\]
([Kernel of the mixed Hessian](./bent_chain_mixed_hessian_kernel.md "why")).

Differentiation gives
\[
\nabla_{x_1}\Phi_{1,m}(x^0,y^0)
=
\frac{x_1^0-y_1^0}{|x_1^0-y_1^0|}
=
-\frac{z_2^0-z_1^0}{|z_2^0-z_1^0|}
=
-u_1
\]
and
\[
\nabla_{y_m}\Phi_{1,m}(x^0,y^0)
=
\frac{y_m^0-x_m^0}{|y_m^0-x_m^0|}
=
\frac{z_{2m}^0-z_{2m-1}^0}{|z_{2m}^0-z_{2m-1}^0|}
=
u_{2m-1}.
\]
Hence
\[
\begin{aligned}
\nabla_x\Phi_{1,m}(x^0,y^0)^Tv_0
&=
\nabla_{x_1}\Phi_{1,m}(x^0,y^0)\cdot u_1
=-|u_1|^2
=-1,\\
w_0^T\nabla_y\Phi_{1,m}(x^0,y^0)
&=
u_{2m-1}\cdot\nabla_{y_m}\Phi_{1,m}(x^0,y^0)
=|u_{2m-1}|^2
=1.
\end{aligned}
\]

Let
\[
M
:=
\begin{pmatrix}
0 & \nabla_x\Phi_{1,m}(x^0,y^0)^T\\
\nabla_y\Phi_{1,m}(x^0,y^0) & H
\end{pmatrix}.
\]
Suppose that \(\lambda\in\mathbb R\) and \(v\in(\mathbb R^d)^m\) satisfy
\[
M\binom{\lambda}{v}=0.
\]
The lower block equation is
\[
\lambda\nabla_y\Phi_{1,m}(x^0,y^0)+Hv=0.
\]
Multiplying this equation on the left by \(w_0^T\) gives
\[
0
=
w_0^T\bigl(\lambda\nabla_y\Phi_{1,m}(x^0,y^0)+Hv\bigr)
=
\lambda w_0^T\nabla_y\Phi_{1,m}(x^0,y^0)+w_0^THv.
\]
Since \(w_0\in\ker H^T\),
\[
w_0^TH
=
(H^Tw_0)^T
=0,
\]
while the preceding gradient calculation gives
\[
w_0^T\nabla_y\Phi_{1,m}(x^0,y^0)=1.
\]
Consequently,
\[
0
=
\lambda w_0^T\nabla_y\Phi_{1,m}(x^0,y^0)+w_0^THv
=
\lambda,
\]
so \(\lambda=0\), and the lower block equation reduces to \(Hv=0\).
Therefore \(v=cv_0\) for some \(c\in\mathbb R\), while the upper block equation gives
\[
0
=
\nabla_x\Phi_{1,m}(x^0,y^0)^Tv
=
c\nabla_x\Phi_{1,m}(x^0,y^0)^Tv_0
=
-c.
\]
Thus \(c=0\), so \(M\) has trivial kernel and is invertible.
Therefore \(\det M\ne0\).
\(\square\)
