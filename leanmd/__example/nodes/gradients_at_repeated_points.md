# Gradients at \(x^0=(p,\ldots,p)\) and \(y^0=(q,\ldots,q)\)

Let \(m\ge2\), let \(d\ge1\), let \(a>0\), and put \(h_a(z):=|z|^a\).
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
g:=Dh_a(v).
\]
Define
\[
\alpha:=(1,2,\ldots,2)^T,
\qquad
\beta:=(2,\ldots,2,1)^T.
\]
Then
\[
\begin{aligned}
\nabla_x\Phi_{a,m}(x^0,y^0)&=-\alpha\otimes g=-(g,2g,\ldots,2g),\\
\nabla_y\Phi_{a,m}(x^0,y^0)&=\beta\otimes g=(2g,\ldots,2g,g).
\end{aligned}
\]

## Proof

For every edge term \(h_a(x_j-y_i)\), evaluation at \((x^0,y^0)\) gives
\[
D_{x_j}h_a(x_j-y_i)=Dh_a(-v)=-g,
\qquad
D_{y_i}h_a(x_j-y_i)=-Dh_a(-v)=g.
\]
Therefore
\[
\begin{aligned}
\nabla_{x_1}\Phi_{a,m}
&=Dh_a(x_1-y_1)=-g,\\
\nabla_{x_i}\Phi_{a,m}
&=Dh_a(x_i-y_{i-1})+Dh_a(x_i-y_i)=-2g
&& (2\le i\le m),\\
\nabla_{y_i}\Phi_{a,m}
&=-Dh_a(x_i-y_i)-Dh_a(x_{i+1}-y_i)=2g
&& (1\le i\le m-1),\\
\nabla_{y_m}\Phi_{a,m}
&=-Dh_a(x_m-y_m)=g,
\end{aligned}
\]
where all derivatives are evaluated at \((x^0,y^0)\).
Collecting these block components gives the stated formulas.
