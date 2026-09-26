export function sourceMapPlugin(markdown) {
  markdown.core.ruler.after("block", "proofmd_source_map", (state) => {
    for (const token of state.tokens) {
      if (!token.block || !Array.isArray(token.map) || token.map.length !== 2) {
        continue;
      }

      const [startLine, endLine] = token.map;
      token.attrSet("data-source-start-line", String(startLine + 1));
      token.attrSet("data-source-end-line", String(endLine));
    }
  });
}
