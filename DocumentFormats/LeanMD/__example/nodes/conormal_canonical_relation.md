# The Conormal Canonical Relation

Let \(U,V\subset\mathbb R^n\) be open, let \(\Phi\in C^\infty(U\times V;\mathbb R)\), and fix \(r\in\mathbb R\).
Define the level hypersurface
\[
\Sigma_r:=\{(x,y)\in U\times V:\Phi(x,y)=r\}.
\]
Suppose that \(d\Phi\ne0\) on \(\Sigma_r\), so that \(\Sigma_r\) is a smooth hypersurface.

## The conormal bundle

A tangent vector \((\dot x,\dot y)\) to \(\Sigma_r\) at \((x,y)\) satisfies
\[
\nabla_x\Phi(x,y)^T\dot x
+
\nabla_y\Phi(x,y)^T\dot y
=0.
\]
A conormal covector is a covector that annihilates every such tangent vector.
Since \(\Sigma_r\) has codimension one, every nonzero conormal covector is a nonzero scalar multiple of \(d\Phi(x,y)\).
Thus it has the form
\[
\xi=\tau\nabla_x\Phi(x,y),
\qquad
\zeta=\tau\nabla_y\Phi(x,y),
\qquad \tau\ne0.
\]
The collection of these covectors over all \((x,y)\in\Sigma_r\) is the nonzero conormal bundle of \(\Sigma_r\).

## The canonical relation

The conormal bundle initially records the combined covector \((\xi,\zeta)\) over the combined point \((x,y)\).
To regard it as a relation from the \(y\)-cotangent space to the \(x\)-cotangent space, write
\[
\eta:=-\zeta.
\]
The conormal canonical relation
\[
C_r\subset (T^*U\setminus0)\times(T^*V\setminus0)
\]
therefore consists of all pairs \(((x,\xi),(y,\eta))\) for which there is a scalar \(\tau\ne0\) satisfying
\[
\Phi(x,y)=r,
\]
\[
\xi=\tau\nabla_x\Phi(x,y),
\qquad
\eta=-\tau\nabla_y\Phi(x,y).
\]
The minus sign does not add a new geometric condition; it is the standard sign convention used when a conormal bundle is viewed as a relation between the input and output cotangent spaces of an operator.

Thus \(C_r\) records both an incidence \(\Phi(x,y)=r\) and the corresponding normal directions at \(x\) and \(y\).
For the generalized Radon transform associated with \(\Phi(x,y)=r\), membership of \(((x,\xi),(y,\eta))\) in \(C_r\) means that the input covector \((y,\eta)\) and the output covector \((x,\xi)\) are connected by that incidence hypersurface.

## Local canonical graphs

There are two natural projections of \(C_r\): one retains \((x,\xi)\), and the other retains \((y,\eta)\).
If both projections are local diffeomorphisms, then either cotangent point determines the other one locally.
In that case \(C_r\) is called a local canonical graph.
