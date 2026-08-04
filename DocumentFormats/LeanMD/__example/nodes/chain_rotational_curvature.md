# Rotational Curvature of the Alternating Chain Phase

Let \(m\ge2\), let \(d\ge1\) be an integer, let \(a>0\), and assume that \((a,d)\ne(1,1)\).
Let \(\mu\) be a compactly supported \(t\)-Frostman probability measure on \(\mathbb R^d\), and assume
\[
t>
\begin{cases}
0,&a\ne1,\\
1,&a=1.
\end{cases}
\]
For \(x=(x_1,\ldots,x_m)\) and \(y=(y_1,\ldots,y_m)\), define
\[
\Phi_{a,m}(x,y)
=
\sum_{i=1}^m|x_i-y_i|^a
+
\sum_{i=1}^{m-1}|x_{i+1}-y_i|^a.
\]

## Proposition

There exist points
\[
p_1,\ldots,p_m,q_1,\ldots,q_m\in\operatorname{spt}\mu
\]
and open neighborhoods \(U_i\) of \(p_i\) and \(V_i\) of \(q_i\) such that, with
\[
U:=U_1\times\cdots\times U_m,
\qquad
V:=V_1\times\cdots\times V_m,
\]
the phase \(\Phi_{a,m}\) is smooth on \(U\times V\) and
\[
\det
\begin{pmatrix}
0 & \nabla_x\Phi_{a,m}(x,y)^T\\
\nabla_y\Phi_{a,m}(x,y) & (\Phi_{a,m})_{yx}(x,y)
\end{pmatrix}
\ne0,\qquad (x,y)\in U\times V.
\tag{ND}
\]

## Proof

For \(z\in\mathbb R^d\setminus\{0\}\), put
\[
\begin{aligned}
h_a(z)&:=|z|^a,\\
g_a(z)&:=Dh_a(z)=a|z|^{a-2}z,\\
A_a(z)&:=D^2h_a(z)=a|z|^{a-2}I_d+a(a-2)|z|^{a-4}zz^T.
\end{aligned}
\]
If \(u=z/|z|\), then the [radial eigenvalue](./radial_and_tangential_hessian_eigenvalues.md "why") of \(A_a(z)\) is \(a(a-1)|z|^{a-2}\), while every [tangential eigenvalue](./radial_and_tangential_hessian_eigenvalues.md "why") is \(a|z|^{a-2}\).
Thus \(A_a(z)\) is invertible when \(a\ne1\), whereas
\[
A_1(z)=\frac1{|z|}(I_d-u\otimes u),
\qquad
\ker A_1(z)=\mathbb Ru.
\]

Define
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
\Omega_m\cap(\operatorname{spt}\mu)^{2m}\ne\varnothing
\]
([Why this intersection is nonempty](./omega_meets_product_support.md "why")).

For \((x,y)\in\Omega_m\), introduce the alternating vertices
\[
z_{2i-1}:=x_i,
\qquad
z_{2i}:=y_i,
\qquad 1\le i\le m,
\]
and set
\[
e_j:=z_{j+1}-z_j,
\qquad
A_j:=A_a(e_j),
\qquad 1\le j\le2m-1.
\]
On \(\Omega_m\), the only nonzero \(d\times d\) blocks of \(H:=(\Phi_{a,m})_{yx}\) are
\[
H_{i,i}=-A_{2i-1},
\qquad 1\le i\le m,
\]
and
\[
H_{i,i+1}=-A_{2i},
\qquad 1\le i\le m-1.
\]
Consequently,
\[
H
=-
\begin{pmatrix}
A_1&A_2&&&\\
&A_3&A_4&&\\
&&\ddots&\ddots&\\
&&&A_{2m-3}&A_{2m-2}\\
&&&&A_{2m-1}
\end{pmatrix}.
\]

### The case \(a\ne1\)

Since \(t>0\), the Frostman estimate implies that \(\operatorname{spt}\mu\) contains two distinct points \(\xi\) and \(\eta\).
Set
\[
p_i:=\xi,
\qquad
q_i:=\eta,
\qquad 1\le i\le m.
\]
Define
\[
x^0:=(p_1,\ldots,p_m),
\qquad
y^0:=(q_1,\ldots,q_m).
\]
Note that \((x^0,y^0)\in\Omega_m\) since \(\xi\ne\eta\).
At \((x^0,y^0)\), the matrix
\[
\begin{pmatrix}
0 & \nabla_x\Phi_{a,m}(x^0,y^0)^T\\
\nabla_y\Phi_{a,m}(x^0,y^0) & H(x^0,y^0)
\end{pmatrix}
\]
in \((\mathrm{ND})\) is invertible ([Nondegeneracy for \(a\ne1\)](./straight_chain_nondegeneracy.md "why")).
Since \(\Omega_m\) is open and \(\Phi_{a,m}\) is smooth on it, continuity gives coordinate neighborhoods \(U_i\) of \(p_i\) and \(V_i\) of \(q_i\) whose product \(U\times V\) is contained in \(\Omega_m\) and satisfies \((\mathrm{ND})\).

### The case \(a=1\)

The excluded pair \((a,d)=(1,1)\) gives \(d\ge2\), and the assumption on \(t\) gives \(t>1\).
The Frostman estimate yields
\[
\dim_H(\operatorname{spt}\mu)\ge t>1,
\]
so \(\operatorname{spt}\mu\) is not contained in an affine line.
Choose noncollinear points \(\xi,\eta,\zeta\in\operatorname{spt}\mu\), and define
\[
z_j^0
=
\begin{cases}
\xi,&j\equiv1\pmod3,\\
\eta,&j\equiv2\pmod3,\\
\zeta,&j\equiv0\pmod3.
\end{cases}
\]
Set
\[
p_i:=z_{2i-1}^0,
\qquad
q_i:=z_{2i}^0,
\qquad 1\le i\le m.
\]
Define
\[
x^0:=(p_1,\ldots,p_m),
\qquad
y^0:=(q_1,\ldots,q_m).
\]
Note that \((x^0,y^0)\in\Omega_m\) since the noncollinear points \(\xi,\eta,\zeta\) are pairwise distinct.
Moreover, consecutive edges have nonparallel directions.
At \((x^0,y^0)\), the matrix in \((\mathrm{ND})\) is invertible ([Nondegeneracy for \(a=1\)](./bent_chain_nondegeneracy.md "why")).
Since \(\Omega_m\) is open and \(\Phi_{a,m}\) is smooth on it, continuity gives coordinate neighborhoods \(U_i\) of \(p_i\) and \(V_i\) of \(q_i\) whose product \(U\times V\) is contained in \(\Omega_m\) and satisfies \((\mathrm{ND})\).

This proves the proposition. \(\square\)
