# Boundedness of a Continuous Function

Suppose, contrary to the [definition of boundedness](../root.md "recall"), that \(f\) is not bounded.
Then, for every integer \(n\ge1\), we can choose \(x_n\in[a,b]\) such that

\[
|f(x_n)|\ge n.
\]

Since this sequence lies in the closed interval \([a,b]\), there exist a strictly increasing sequence of positive integers \((n_k)\) and a point \(x\in[a,b]\) such that

\[
x_{n_k}\longrightarrow x
\]

([Convergent Subsequences in a Closed Interval](./bolzano_weierstrass_subsequence.md "why")).
Because \(f\) is continuous and \(x_{n_k}\to x\), we have \(f(x_{n_k})\to f(x)\) ([Sequential Criterion for Continuity](./sequential_continuity.md "why")).
Every convergent real sequence is bounded, so \((f(x_{n_k}))\) must be bounded ([Convergent Real Sequences Are Bounded](./convergent_sequence_is_bounded.md "why")).

However, the choice \(|f(x_n)|\ge n\) for every \(n\) gives

\[
|f(x_{n_k})|\ge n_k\ge k,
\]
which contradicts the boundedness of the sequence of function values.
Therefore \(f\) is bounded on \([a,b]\).
