# Nested Interval Theorem

Let \(I_k=[a_k,b_k]\) be a sequence of nonempty closed intervals satisfying

\[
I_1\supseteq I_2\supseteq I_3\supseteq\cdots.
\]
The set of left endpoints \(A=\{a_k:k\ge1\}\) is nonempty and bounded above by \(b_1\).
By the completeness of \(\mathbb{R}\), \(x=\sup A\) exists.

Fix any positive integer \(m\).
Since \(a_k\le b_m\) for every \(k\), the number \(b_m\) is an upper bound for \(A\), and hence \(x\le b_m\).
Since \(a_m\in A\), we also have \(a_m\le x\).
Therefore

\[
a_m\le x\le b_m,
\]

so \(x\in I_m\).
Because \(m\) was arbitrary, \(x\) belongs to every \(I_m\), and thus the intervals have a common point.

Moreover, if the interval lengths \(b_m-a_m\) converge to \(0\), then the common point is unique.
Indeed, if \(x,y\) belong to every \(I_m\), then \(|x-y|\le b_m-a_m\) for every \(m\), so \(x=y\).
