# Mixed-Hessian Block Form and Tensor-Product Inverse

Let \(m\ge2\), let \(d\ge1\), let \(a>0\) with \(a\ne1\), and put \(h_a(z):=|z|^a\).
For
\[
\Phi_{a,m}(x,y)
=
\sum_{i=1}^m h_a(x_i-y_i)
+
\sum_{i=1}^{m-1}h_a(x_{i+1}-y_i),
\]
fix distinct \(p,q\in\mathbb R^d\) and set
\[
x^0:=(p,\ldots,p),
\qquad
y^0:=(q,\ldots,q),
\qquad
v:=q-p,
\qquad
A:=D^2h_a(v).
\]
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
\end{pmatrix},
\qquad
H:=(\Phi_{a,m})_{yx}(x^0,y^0).
\]
Then
\[
H=-U_m\otimes A,
\qquad
H^{-1}=-(U_m^{-1}\otimes A^{-1}).
\]

## Proof

Since
\[
D_yD_xh_a(x-y)=-D^2h_a(x-y)
\]
and \(D^2h_a(-v)=D^2h_a(v)=A\), the nonzero blocks of \(H\) are
\[
H_{i,i}=-A,
\qquad 1\le i\le m,
\]
and
\[
H_{i,i+1}=-A,
\qquad 1\le i\le m-1.
\]
All other blocks vanish, so
\[
H=-U_m\otimes A.
\]

The matrix \(U_m\) is upper triangular with diagonal entries equal to \(1\), and \(A\) is invertible because \(a\ne1\).
The tensor product is determined by
\[
(U_m\otimes A)(\xi\otimes w)=U_m\xi\otimes Aw.
\]
For linear maps \(S\) and \(B\) of the same respective spaces,
\[
\begin{aligned}
(S\otimes B)(U_m\otimes A)(\xi\otimes w)
&=SU_m\xi\otimes BAw\\
&=((SU_m)\otimes(BA))(\xi\otimes w).
\end{aligned}
\]
Since pure tensors span \(\mathbb R^m\otimes\mathbb R^d\),
\[
(S\otimes B)(U_m\otimes A)=(SU_m)\otimes(BA).
\]
Taking \(S=U_m^{-1}\) and \(B=A^{-1}\) gives
\[
(U_m\otimes A)^{-1}=U_m^{-1}\otimes A^{-1}.
\]
Therefore
\[
H^{-1}=-(U_m^{-1}\otimes A^{-1}).
\]
