# Uniform Continuity of a Continuous Function

Suppose the [definition of uniform continuity](../root.md "recall") fails.
Then there exists \(\varepsilon_0>0\) such that, for every \(\delta>0\), one can find \(x,y\in[a,b]\) satisfying

\[
|x-y|<\delta,
\qquad
|f(x)-f(y)|\ge\varepsilon_0.
\]

For every positive integer \(n\), apply this statement with \(\delta=1/n\) and choose \(x_n,y_n\in[a,b]\) such that

\[
|x_n-y_n|<\frac1n,
\qquad
|f(x_n)-f(y_n)|\ge\varepsilon_0.
\]
Since \((x_n)\) lies in a closed interval, it has a subsequence \((x_{n_k})\) that converges to a point \(x\in[a,b]\) ([Convergent Subsequences in a Closed Interval](./bolzano_weierstrass_subsequence.md "why")).

Since \(|x_{n_k}-y_{n_k}|<1/n_k\to0\), we also have \(y_{n_k}\to x\) ([Nearby Sequences Have the Same Limit](./nearby_sequences_share_limit.md "why")).
Applying continuity to both sequences gives

\[
f(x_{n_k})\longrightarrow f(x),
\qquad
f(y_{n_k})\longrightarrow f(x)
\]

([Sequential Criterion for Continuity](./sequential_continuity.md "why")).
Therefore

\[
|f(x_{n_k})-f(y_{n_k})|\longrightarrow0.
\]

This contradicts the choice that makes this quantity at least \(\varepsilon_0\) for every \(k\).
Therefore \(f\) is uniformly continuous on \([a,b]\).
