# Nondegeneracy for \(a\ne1\)

Let \(m\ge2\), let \(d\ge1\) be an integer, and let \(a>0\) with \(a\ne1\).
For \(x=(x_1,\ldots,x_m),y=(y_1,\ldots,y_m)\in(\mathbb R^d)^m\), define
\[
\Phi_{a,m}(x,y)
=
\sum_{i=1}^m|x_i-y_i|^a
+
\sum_{i=1}^{m-1}|x_{i+1}-y_i|^a.
\]

## Proposition

Let \(p,q\in\mathbb R^d\) be distinct.
Define
\[
x^0:=(p,\ldots,p),
\qquad
y^0:=(q,\ldots,q).
\]
Then
\[
\det
\begin{pmatrix}
0 & \nabla_x\Phi_{a,m}(x^0,y^0)^T\\
\nabla_y\Phi_{a,m}(x^0,y^0) & (\Phi_{a,m})_{yx}(x^0,y^0)
\end{pmatrix}
\ne0.
\]

## Proof

Put \(h_a(z):=|z|^a\), and set
\[
v:=q-p,
\qquad
A:=D^2h_a(v).
\]
Since \(h_a\) is even, \(D^2h_a(-v)=A\).
The radial eigenvalue of \(A\) is \(a(a-1)|v|^{a-2}\), and every tangential eigenvalue is \(a|v|^{a-2}\), so \(A\) is invertible because \(a\ne1\).

Let
\[
U_m
:=
\begin{pmatrix}
1&1&&&\\
&1&1&&\\
&&\ddots&\ddots&\\
&&&1&1\\
&&&&1
\end{pmatrix}.
\]
For \(H:=(\Phi_{a,m})_{yx}(x^0,y^0)\),
\[
H=-U_m\otimes A,
\qquad
H^{-1}=-(U_m^{-1}\otimes A^{-1})
\]
([Mixed-Hessian block form and tensor-product inverse](./tensor_product_inverse.md "why")).

Set
\[
g:=Dh_a(v)=a|v|^{a-2}v.
\]
Let \(\mathbf1:=(1,\ldots,1)^T\in\mathbb R^m\), and put
\[
\alpha:=U_m^T\mathbf1=(1,2,\ldots,2)^T,
\qquad
\beta:=U_m\mathbf1=(2,\ldots,2,1)^T.
\]
Then
\[
\begin{aligned}
\nabla_x\Phi_{a,m}(x^0,y^0)&=-\alpha\otimes g=-(g,2g,\ldots,2g),\\
\nabla_y\Phi_{a,m}(x^0,y^0)&=\beta\otimes g=(2g,\ldots,2g,g).
\end{aligned}
\]
([Gradient calculation](./gradients_at_repeated_points.md "why")).
Since \(H\) is invertible, the Schur-complement formula gives
\[
\begin{aligned}
&\det
\begin{pmatrix}
0 & \nabla_x\Phi_{a,m}(x^0,y^0)^T\\
\nabla_y\Phi_{a,m}(x^0,y^0) & H
\end{pmatrix}\\
&\qquad=
-\det(H)\,
\nabla_x\Phi_{a,m}(x^0,y^0)^T
H^{-1}
\nabla_y\Phi_{a,m}(x^0,y^0).
\end{aligned}
\]
Since \(\det(H)\ne0\), it remains to show that
\[
\nabla_x\Phi_{a,m}(x^0,y^0)^T
H^{-1}
\nabla_y\Phi_{a,m}(x^0,y^0)
\ne0.
\]
Using the tensor-product formulas above,
\[
\begin{aligned}
&\nabla_x\Phi_{a,m}(x^0,y^0)^T
H^{-1}
\nabla_y\Phi_{a,m}(x^0,y^0)\\
&\quad=
(-\alpha\otimes g)^T
\bigl(-(U_m^{-1}\otimes A^{-1})\bigr)
(\beta\otimes g)\\
&\quad=
(\alpha^T\otimes g^T)
(U_m^{-1}\otimes A^{-1})
(\beta\otimes g)\\
&\quad=
(\alpha^TU_m^{-1}\beta)
\otimes
(g^TA^{-1}g)\\
&\quad=
\bigl(\alpha^TU_m^{-1}\beta\bigr)
\bigl(g^TA^{-1}g\bigr).
\end{aligned}
\]
The first factor is
\[
\alpha^TU_m^{-1}\beta
=(U_m^T\mathbf1)^TU_m^{-1}(U_m\mathbf1)
=\mathbf1^TU_m\mathbf1
=2m-1.
\]
The second factor satisfies
\[
A^{-1}g=\frac{v}{a-1},
\qquad
g^TA^{-1}g=\frac{a}{a-1}|v|^a
\]
([Radial-factor calculation](./radial_factor_for_power_hessian.md "why")).
Consequently,
\[
\nabla_x\Phi_{a,m}(x^0,y^0)^T
H^{-1}
\nabla_y\Phi_{a,m}(x^0,y^0)
=(2m-1)\frac{a}{a-1}|v|^a
\ne0.
\]
Thus the block determinant above is nonzero.
\(\square\)
