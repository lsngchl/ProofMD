# Convergent Real Sequences Are Bounded

Let \((a_n)\) be a real sequence converging to \(L\in\mathbb{R}\).
Applying the definition of convergence with \(\varepsilon=1\), there exists a positive integer \(N\) such that, whenever \(n\ge N\),

\[
|a_n-L|<1.
\]
Therefore, by the triangle inequality,

\[
|a_n|\le |a_n-L|+|L|<1+|L|.
\]
The first \(N-1\) terms form a finite set, so their absolute values also have an upper bound.
If \(N=1\), set \(M_0=0\); if \(N>1\), set

\[
M_0=\max\{|a_1|,\ldots,|a_{N-1}|\}.
\]
Now let

\[
M=\max\{M_0,1+|L|\}.
\]
Then \(|a_n|\le M\) for every positive integer \(n\).
Therefore every convergent real sequence is bounded.
