# Sequential Criterion for Continuity

Let \(f:[a,b]\to\mathbb{R}\) be [continuous](../root.md "recall"), and let \((x_n)\) be a sequence in \([a,b]\) that converges to \(x\in[a,b]\).
Then

\[
f(x_n)\longrightarrow f(x).
\]

Let \(\varepsilon>0\).
Since \(f\) is continuous at \(x\), there exists \(\delta>0\) such that

\[
|y-x|<\delta
\quad\Longrightarrow\quad
|f(y)-f(x)|<\varepsilon
\]

for every \(y\in[a,b]\).
Since \(x_n\to x\), we have \(|x_n-x|<\delta\) for all sufficiently large \(n\).
Therefore \(|f(x_n)-f(x)|<\varepsilon\) for all sufficiently large \(n\), which proves the desired convergence.
