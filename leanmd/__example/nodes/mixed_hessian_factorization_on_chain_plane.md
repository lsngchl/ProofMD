# Factorization of the Mixed Hessian on the Chain Plane

Let \(m\ge2\), let \(P\) be a two-dimensional Euclidean space, and let \(e_1,\ldots,e_{2m-1}\in P\setminus\{0\}\).
For each \(j\), let \(n_j\in P\) be a unit vector and put
\[
\alpha_j:=|e_j|^{-1/2},
\qquad
A_j:=\alpha_j^2n_jn_j^T.
\]
Define \(H_P:P^m\to P^m\) by
\[
H_P
=
-\begin{pmatrix}
A_1&A_2&&&\\
&A_3&A_4&&\\
&&\ddots&\ddots&\\
&&&A_{2m-3}&A_{2m-2}\\
&&&&A_{2m-1}
\end{pmatrix}.
\]
Define linear maps
\[
R_X,R_Y:\mathbb R^{2m-1}\to P^m
\]
as follows.
For \(c=(c_1,\ldots,c_{2m-1})\in\mathbb R^{2m-1}\), set
\[
\begin{aligned}
(R_Xc)_1
&:=\alpha_1c_1n_1,\\
(R_Xc)_i
&:=\alpha_{2i-2}c_{2i-2}n_{2i-2}
+\alpha_{2i-1}c_{2i-1}n_{2i-1},
&&2\le i\le m,
\end{aligned}
\]
and
\[
\begin{aligned}
(R_Yc)_i
&:=\alpha_{2i-1}c_{2i-1}n_{2i-1}
+\alpha_{2i}c_{2i}n_{2i},
&&1\le i\le m-1,\\
(R_Yc)_m
&:=\alpha_{2m-1}c_{2m-1}n_{2m-1}.
\end{aligned}
\]
Equip \(P^m\) with the product inner product induced by the inner product on \(P\), and equip \(\mathbb R^{2m-1}\) with its standard inner product.
The transposes are the linear maps
\[
R_X^T,R_Y^T:P^m\to\mathbb R^{2m-1}.
\]

## Lemma

\[
H_P=-R_YR_X^T.
\]

## Proof

Let \(v=(v_1,\ldots,v_m)\in P^m\).
For \(c=(c_1,\ldots,c_{2m-1})\in\mathbb R^{2m-1}\), the definition of the transpose gives
\[
\langle R_Xc,v\rangle_{P^m}
=
\langle c,R_X^Tv\rangle_{\mathbb R^{2m-1}}.
\]
Expanding the left-hand side using the definition of \(R_X\) gives
\[
\begin{aligned}
\langle R_Xc,v\rangle_{P^m}
&=
\sum_{i=1}^m\langle(R_Xc)_i,v_i\rangle_P\\
&=
\alpha_1c_1n_1^Tv_1
+
\sum_{i=2}^m
\left(
\alpha_{2i-2}c_{2i-2}n_{2i-2}^Tv_i
+
\alpha_{2i-1}c_{2i-1}n_{2i-1}^Tv_i
\right)\\
&=
\sum_{i=1}^m
c_{2i-1}\left(\alpha_{2i-1}n_{2i-1}^Tv_i\right)
+
\sum_{i=1}^{m-1}
c_{2i}\left(\alpha_{2i}n_{2i}^Tv_{i+1}\right).
\end{aligned}
\]
The right-hand side is
\[
\langle c,R_X^Tv\rangle_{\mathbb R^{2m-1}}
=
\sum_{j=1}^{2m-1}c_j(R_X^Tv)_j.
\]
Since these expressions agree for every \(c\in\mathbb R^{2m-1}\), comparison of the coefficients of \(c_{2i-1}\) and \(c_{2i}\) gives
\[
(R_X^Tv)_{2i-1}
=
\alpha_{2i-1}n_{2i-1}^Tv_i,
\qquad 1\le i\le m,
\]
and
\[
(R_X^Tv)_{2i}
=
\alpha_{2i}n_{2i}^Tv_{i+1},
\qquad 1\le i\le m-1.
\]
For \(1\le i\le m-1\), substituting these coordinates into the definition of \(R_Y\) gives
\[
\begin{aligned}
(R_YR_X^Tv)_i
&=
\alpha_{2i-1}n_{2i-1}(R_X^Tv)_{2i-1}
+\alpha_{2i}n_{2i}(R_X^Tv)_{2i}\\
&=
\alpha_{2i-1}^2n_{2i-1}n_{2i-1}^Tv_i
+\alpha_{2i}^2n_{2i}n_{2i}^Tv_{i+1}\\
&=
A_{2i-1}v_i+A_{2i}v_{i+1}.
\end{aligned}
\]
For the last block,
\[
\begin{aligned}
(R_YR_X^Tv)_m
&=
\alpha_{2m-1}n_{2m-1}(R_X^Tv)_{2m-1}\\
&=
\alpha_{2m-1}^2n_{2m-1}n_{2m-1}^Tv_m\\
&=
A_{2m-1}v_m.
\end{aligned}
\]
On the other hand, the block definition of \(H_P\) gives
\[
(H_Pv)_i
=
-A_{2i-1}v_i-A_{2i}v_{i+1},
\qquad 1\le i\le m-1,
\]
and
\[
(H_Pv)_m=-A_{2m-1}v_m.
\]
Thus \(H_Pv=-R_YR_X^Tv\) for every \(v\in P^m\), and therefore
\[
H_P=-R_YR_X^T.
\]
\(\square\)
