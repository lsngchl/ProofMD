# Two Consequences of Continuity on a Closed Interval

Let \(a,b\in\mathbb{R}\) satisfy \(a\le b\), and let

\[
f:[a,b]\longrightarrow\mathbb{R}
\]

be continuous.
Here continuity means that for every \(x\in[a,b]\) and every \(\varepsilon>0\), there exists \(\delta>0\) such that

\[
y\in[a,b],\quad |x-y|<\delta
\quad\Longrightarrow\quad
|f(x)-f(y)|<\varepsilon
\]

holds.

The function \(f\) is **bounded** if there exists \(M\ge 0\) such that \(|f(x)|\le M\) for every \(x\in[a,b]\).
The function \(f\) is **uniformly continuous** if, for every \(\varepsilon>0\), one can choose a single \(\delta>0\) that works simultaneously for all \(x,y\in[a,b]\).

We will prove the following two conclusions.

1. The function \(f\) is bounded on \([a,b]\) ([Boundedness of a Continuous Function](./nodes/boundedness_argument.md "why")).
2. The function \(f\) is uniformly continuous on \([a,b]\) ([Uniform Continuity of a Continuous Function](./nodes/uniform_continuity_argument.md "why")).
