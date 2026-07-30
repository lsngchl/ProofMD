# Kernel of the Mixed Hessian for \(a=1\)

Let \(d\ge2\) and \(m\ge2\).
For \(x=(x_1,\ldots,x_m),y=(y_1,\ldots,y_m)\in(\mathbb R^d)^m\), define
\[
\Phi_{1,m}(x,y)
=
\sum_{i=1}^m|x_i-y_i|
+
\sum_{i=1}^{m-1}|x_{i+1}-y_i|.
\]

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
y^0:=(z_2^0,z_4^0,\ldots,z_{2m}^0),
\]

## Lemma

For \(1\le j\le2m-1\), put
\[
e_j:=z_{j+1}^0-z_j^0,
\qquad
u_j:=\frac{e_j}{|e_j|},
\qquad 1\le j\le2m-1.
\]
Let
\[
H:=(\Phi_{1,m})_{yx}(x^0,y^0).
\]
Then
\[
\operatorname{rank}H=md-1.
\]
Moreover,
\[
\ker H
=
\operatorname{span}\{(u_1,0,\ldots,0)\},
\]
and
\[
\ker H^T
=
\operatorname{span}\{(0,\ldots,0,u_{2m-1})\}.
\]

## Proof

Let \(P\subset\mathbb R^d\) be the two-dimensional linear subspace parallel to the affine plane containing the chain, and let \(Q:=P^\perp\).
For \(z\in\mathbb R^d\setminus\{0\}\), define \(h(z):=|z|\).
For \(1\le j\le2m-1\), set
\[
A_j
:=
D^2h(e_j)
=
\frac1{|e_j|}(I_d-u_j\otimes u_j).
\]
The mixed Hessian has the block form
\[
H
=
-\begin{pmatrix}
A_1&A_2&&&\\
&A_3&A_4&&\\
&&\ddots&\ddots&\\
&&&A_{2m-3}&A_{2m-2}\\
&&&&A_{2m-1}
\end{pmatrix}.
\]
Since every \(u_j\) belongs to \(P\), the decomposition
\[
(\mathbb R^d)^m=P^m\oplus Q^m
\]
reduces \(H\).

For \(q\in Q\), the inclusions \(u_j\in P\) and \(Q=P^\perp\) give
\[
(u_j\otimes u_j)q
=
u_j(u_j\cdot q)
=0.
\]
Therefore
\[
A_jq
=
\frac1{|e_j|}\bigl(q-(u_j\otimes u_j)q\bigr)
=
\frac1{|e_j|}q.
\]
Since this holds for every \(q\in Q\),
\[
A_j|_Q=|e_j|^{-1}I_Q.
\]
Consequently,
\[
H|_{Q^m}=-T\otimes I_Q,
\qquad
T
:=
\begin{pmatrix}
|e_1|^{-1}&|e_2|^{-1}&&&\\
&|e_3|^{-1}&|e_4|^{-1}&&\\
&&\ddots&\ddots&\\
&&&|e_{2m-3}|^{-1}&|e_{2m-2}|^{-1}\\
&&&&|e_{2m-1}|^{-1}
\end{pmatrix}.
\]
The matrix \(T\) is upper triangular with nonzero diagonal entries, so \(H|_{Q^m}\) is invertible.

For each \(j\), choose a unit vector \(n_j\in P\) perpendicular to \(u_j\).
Since \(\{u_j,n_j\}\) is an orthonormal basis of \(P\), every \(p\in P\) satisfies
\[
p
=
(u_j\cdot p)u_j+(n_j\cdot p)n_j.
\]
Hence
\[
\begin{aligned}
A_jp
&=
\frac1{|e_j|}\bigl(p-(u_j\otimes u_j)p\bigr)\\
&=
\frac1{|e_j|}\bigl(p-(u_j\cdot p)u_j\bigr)\\
&=
\frac1{|e_j|}(n_j\cdot p)n_j\\
&=
\frac1{|e_j|}n_jn_j^Tp.
\end{aligned}
\]
Since this holds for every \(p\in P\),
\[
A_j|_P=|e_j|^{-1}n_jn_j^T.
\]
For \(c=(c_1,\ldots,c_{2m-1})\in\mathbb R^{2m-1}\), define \(R_Xc,R_Yc\in P^m\) by
\[
\begin{aligned}
(R_Xc)_1
&:=|e_1|^{-1/2}c_1n_1,\\
(R_Xc)_i
&:=|e_{2i-2}|^{-1/2}c_{2i-2}n_{2i-2}
+|e_{2i-1}|^{-1/2}c_{2i-1}n_{2i-1},
&&2\le i\le m,
\end{aligned}
\]
and
\[
\begin{aligned}
(R_Yc)_i
&:=|e_{2i-1}|^{-1/2}c_{2i-1}n_{2i-1}
+|e_{2i}|^{-1/2}c_{2i}n_{2i},
&&1\le i\le m-1,\\
(R_Yc)_m
&:=|e_{2m-1}|^{-1/2}c_{2m-1}n_{2m-1}.
\end{aligned}
\]
The block formula for \(H\) gives
\[
H|_{P^m}=-R_YR_X^T
\]
([Detailed block calculation](./mixed_hessian_factorization_on_chain_plane.md "why")).

At every internal vertex, the two adjacent edge directions are not parallel, so the corresponding vectors \(n_j\) are linearly independent in \(P\).
It follows block by block from the formulas above that both \(R_X\) and \(R_Y\) are injective.
Hence \(R_X^T\) is surjective, and therefore
\[
\operatorname{rank}(H|_{P^m})=2m-1.
\]

Since \(R_Y\) is injective,
\[
\ker(H|_{P^m})=\ker R_X^T.
\]
If \(v=(v_1,\ldots,v_m)\in\ker R_X^T\), then
\[
n_1^Tv_1=0
\]
and, for \(2\le i\le m\),
\[
n_{2i-2}^Tv_i=n_{2i-1}^Tv_i=0
\]
(For the calculation of the coordinates of \(R_X^Tv\), see [Factorization of the Mixed Hessian on the Chain Plane](./mixed_hessian_factorization_on_chain_plane.md "why")).
The latter two normals are linearly independent, so \(v_i=0\) for \(2\le i\le m\), while \(v_1\in\mathbb Ru_1\).
Thus
\[
\ker(H|_{P^m})
=
\operatorname{span}\{(u_1,0,\ldots,0)\}.
\]
Similarly, since \(R_X\) is injective,
\[
\ker((H|_{P^m})^T)
=
\ker R_Y^T
=
\operatorname{span}\{(0,\ldots,0,u_{2m-1})\}.
\]

The restriction of \(H\) to \(Q^m\) is invertible, so these are also the right and left kernels of \(H\) on \((\mathbb R^d)^m\).
Finally,
\[
\operatorname{rank}H
=
m(d-2)+(2m-1)
=
md-1.
\]
\(\square\)
