# The Two-Set Configuration Theorem

Let \(n\ge1\) and \(1\le k\le n\), and let
\[
\Phi:\mathbb R^n\times\mathbb R^n\longrightarrow\mathbb R^k
\]
be smooth.
For \(\mathbf r\in\mathbb R^k\) and \(\chi\in C_c^\infty(\mathbb R^n\times\mathbb R^n)\), recall the localized generalized Radon transform
\[
T_{\mathbf r}^\chi f(x)
:=
\int_{\{y\in\mathbb R^n:\Phi(x,y)=\mathbf r\}}
f(y)\chi(x,y)\,d\sigma_{x,\mathbf r}(y),
\]
where \(d\sigma_{x,\mathbf r}\) is the induced measure on the level set.
Let \(0\le\alpha_\Phi\le(n-k)/2\).

## Theorem

Source: [Greenleaf, Iosevich, and Taylor, Theorem 1.5](https://arxiv.org/abs/1907.12513).

Suppose that
\[
\operatorname{rank}D_x\Phi(x,y)
=
\operatorname{rank}D_y\Phi(x,y)
=k
\]
and that, locally uniformly in \(\mathbf r\),
\[
T_{\mathbf r}^\chi:
L_s^2(\mathbb R^n)
\longrightarrow
L_{s+\alpha_\Phi}^2(\mathbb R^n)
\]
is bounded for every \(s\in\mathbb R\).
Then, if \(E,F\subset\mathbb R^n\) are compact and
\[
\dim_HE+\dim_HF
>
2n-2\alpha_\Phi,
\]
then the configuration set
\[
\Delta_\Phi(E,F)
:=
\{\Phi(x,y):x\in E,\ y\in F\}
\]
has nonempty interior in \(\mathbb R^k\).

> **Note.** The source uses \(\mathbf t\) where this document uses \(\mathbf r\), and denotes the level set \(\{(x,y):\Phi(x,y)=\mathbf t\}\) by \(Z_{\mathbf t}\).
> Its fiber \(Z_{\mathbf t}^x=\{y:\Phi(x,y)=\mathbf t\}\) is the level set over which the transform integrates.
> The source writes the localized generalized Radon transform as \(\mathcal R_{\mathbf t}\); in the notation used here, this is \(T_{\mathbf r}^\chi\), with the cutoff \(\chi\) displayed explicitly.
> Condition (1.2) corresponds to the displayed rank condition above, and condition (1.6) corresponds to the displayed Sobolev estimate.
> Condition (1.7) is not an additional hypothesis: it is the same estimate rewritten using \(\beta_\Phi=(n-k)/2-\alpha_\Phi\).

## Application here

In the parent document, the output is scalar, so \(\mathbf r=r\) and \(k=1\), while the two ambient spaces are identified with \(\mathbb R^{md}\), so \(n=md\).
Set
\[
\begin{aligned}
K_x
&:=\{x\in U:(x,y)\in\operatorname{spt}\chi\text{ for some }y\in V\}\Subset U,\\
K_y
&:=\{y\in V:(x,y)\in\operatorname{spt}\chi\text{ for some }x\in U\}\Subset V.
\end{aligned}
\]
The operator \(T_r^\chi f\) depends only on the restriction of \(f\) to a neighborhood of \(K_y\), and its output is supported in \(K_x\).
Extend its Schwartz kernel by zero from \(U\times V\) to \(\mathbb R^{md}\times\mathbb R^{md}\), and continue to denote the resulting operator by \(T_r^\chi\).
Choose \(\eta\in C_c^\infty(V)\) equal to \(1\) on a neighborhood of \(K_y\).
Since zero extension from \(U\) is bounded on distributions supported in the fixed compact set \(K_x\), \((\mathrm{SM})\) gives, for every \(f\in H^s(\mathbb R^{md})\),
\[
\begin{aligned}
\|T_r^\chi f\|_{H^{s+(md-1)/2}(\mathbb R^{md})}
&\lesssim
\bigl\|T_r^\chi\bigl((\eta f)|_V\bigr)\bigr\|_{H^{s+(md-1)/2}(U)}\\
&\lesssim
\|(\eta f)|_V\|_{H^s(V)}\\
&\lesssim
\|f\|_{H^s(\mathbb R^{md})}.
\end{aligned}
\]
The middle inequality is exactly \((\mathrm{SM})\), and the constants are locally uniform in \(r\).
Since \(L_s^2=H^s\), this is the Sobolev estimate required by the theorem with
\[
n=md,
\qquad
k=1,
\qquad
\alpha_\Phi=\frac{md-1}{2}.
\]
Taking \(E=A\) and \(F=B\), the dimension condition becomes
\[
\dim_HA+\dim_HB
>
2md-2\left(\frac{md-1}{2}\right)
=
md+1.
\]
Thus \((\mathrm{DIM})\) is exactly the dimension condition required by the theorem.
Therefore
\[
\operatorname{int}\Phi(A,B)\ne\varnothing.
\]
