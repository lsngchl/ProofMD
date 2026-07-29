# Convergent Subsequences in a Closed Interval

Every sequence \((x_n)\) in a closed interval \([a,b]\) has a subsequence that converges to a point in the interval.

If \(a=b\), every term equals \(a\), so the claim is immediate.
Now suppose \(a<b\).
Divide \([a,b]\) into two closed intervals of equal length.
At least one of them contains infinitely many terms of the sequence; denote such a half by \(I_1\).
Repeating this procedure produces nested intervals

\[
I_1\supseteq I_2\supseteq I_3\supseteq\cdots
\]

such that every \(I_k\) contains infinitely many terms of the sequence and has length \((b-a)/2^k\).

By the nested interval theorem, which follows from the completeness of \(\mathbb{R}\), there exists a point \(x\) belonging to every \(I_k\) ([Nested Interval Theorem](./nested_interval_theorem.md "why")).
Choose indices \(n_1<n_2<\cdots\) successively so that \(x_{n_k}\in I_k\).
Since both \(x\) and \(x_{n_k}\) belong to \(I_k\),

\[
|x_{n_k}-x|\le \frac{b-a}{2^k}\longrightarrow0.
\]

Therefore \(x_{n_k}\to x\), and \(x\in I_1\subseteq[a,b]\).
