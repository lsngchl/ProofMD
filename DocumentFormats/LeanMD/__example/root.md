# Finite Sums of Powered Distance Sets

Let \(d\ge1\) be an integer, let \(a>0\), and let \(E\subset\mathbb R^d\) be compact.
Define the powered distance set by
\[
D_a(E):=\{|x-y|^a:x,y\in E\},
\]
For a set \(S\subset\mathbb R\) and an integer \(N\ge1\), write
\[
NS:=\underbrace{S+\cdots+S}_{N\text{ summands}}.
\]

## Theorem

Let \(m\ge2\) and assume that \((a,d)\ne(1,1)\).
If
\[
\dim_HE>\frac d2+\frac1{2m},
\]
then
\[
\operatorname{int}\bigl((2m-1)D_a(E)\bigr)\ne\varnothing.
\]
In particular, \((2m-1)D_a(E)\) contains a nondegenerate interval.

## Proof

Put \(s=\dim_HE\) and choose \(t\) such that
\[
\frac d2+\frac1{2m}<t<s.
\]
Let \(\mu\) be a \(t\)-Frostman probability measure supported on \(E\).
The choice of \(t\) gives
\[
2mt>md+1.
\]

For \(x=(x_1,\ldots,x_m)\) and \(y=(y_1,\ldots,y_m)\), define the alternating-chain phase
\[
\Phi_{a,m}(x,y)
=
\sum_{i=1}^m|x_i-y_i|^a
+
\sum_{i=1}^{m-1}|x_{i+1}-y_i|^a.
\]
This is the total \(a\)-powered edge length of the path
\[
x_1-y_1-x_2-y_2-\cdots-x_m-y_m.
\]

For every \((a,d)\ne(1,1)\), there exist points
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
\ne0,\qquad (x,y)\in U\times V
\tag{ND}
\]
([Rotational Curvature of the Alternating Chain Phase](./nodes/chain_rotational_curvature.md "why")).

Since \(p_i,q_i\in\operatorname{spt}\mu\), every \(U_i\) and \(V_i\) has positive \(\mu\)-measure.
By inner regularity, choose compact sets
\[
E_i\subset E\cap U_i,
\qquad
F_i\subset E\cap V_i
\]
with \(\mu(E_i)>0\) and \(\mu(F_i)>0\).
The normalized restrictions
\[
\mu_i:=\frac{\mu|_{E_i}}{\mu(E_i)},
\qquad
\nu_i:=\frac{\mu|_{F_i}}{\mu(F_i)}
\]
are \(t\)-Frostman probability measures.
Define
\[
A:=E_1\times\cdots\times E_m\subset E^m\cap U,
\qquad
B:=F_1\times\cdots\times F_m\subset E^m\cap V.
\]
For \(x=(x_1,\ldots,x_m)\), the product measure \(\mu_1\otimes\cdots\otimes\mu_m\) satisfies
\[
(\mu_1\otimes\cdots\otimes\mu_m)
\bigl(B_{\mathbb R^{md}}(x,r)\bigr)
\le
\prod_{i=1}^m\mu_i\bigl(B_{\mathbb R^d}(x_i,r)\bigr)
\lesssim r^{mt},
\]
and the same estimate holds for \(\nu_1\otimes\cdots\otimes\nu_m\) on \(B\).
Frostman's lemma therefore gives
\[
\dim_HA\ge mt,
\qquad
\dim_HB\ge mt.
\]
Consequently,
\[
\dim_HA+\dim_HB\ge2mt>md+1.
\tag{DIM}
\]

The smoothness of \(\Phi_{a,m}\) on \(U\times V\) and conditions \((\mathrm{ND})\) and \((\mathrm{DIM})\) imply that \(\Phi_{a,m}(A,B)\) has nonempty interior ([Nonempty Interior of the Configuration Image](./nodes/localized_two_set_configuration_theorem.md "why")).
For \(x=(x_1,\ldots,x_m)\in A\) and \(y=(y_1,\ldots,y_m)\in B\), the inclusions \(A,B\subset E^m\) give \(x_i,y_i\in E\) for every \(i\).
Hence
\[
|x_i-y_i|^a\in D_a(E),\qquad 1\le i\le m,
\]
and
\[
|x_{i+1}-y_i|^a\in D_a(E),\qquad 1\le i\le m-1.
\]
In the sum \(\Phi_{a,m}(x,y)\), there are \(m\) terms of the first kind and \(m-1\) terms of the second kind, so
\[
\Phi_{a,m}(x,y)\in(2m-1)D_a(E).
\]
Since \(x\in A\) and \(y\in B\) were arbitrary,
\[
\Phi_{a,m}(A,B)\subset(2m-1)D_a(E).
\]
Since \(\Phi_{a,m}(A,B)\) has nonempty interior,
\[
\operatorname{int}\bigl((2m-1)D_a(E)\bigr)\ne\varnothing.
\]
This proves the theorem. \(\square\)

## Corollary

Define
\[
\theta_a(d)
:=
\inf\left\{
\theta\in[0,d]:
\begin{array}{l}
\text{every compact }E\subset\mathbb R^d\text{ with }\dim_HE>\theta\\
\text{satisfies }\operatorname{int}(ND_a(E))\ne\varnothing\text{ for some }N\ge1
\end{array}
\right\}.
\]

Given \(s>d/2\), one may choose \(m\) sufficiently large that \(s>d/2+1/(2m)\), and the theorem then applies.
Hence
\[
\theta_a(d)\le\frac d2
\]
for every integer \(d\ge1\) and every \(a>0\) with \((a,d)\ne(1,1)\).
