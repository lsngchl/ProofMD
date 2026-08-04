# LeanMD HTML build

`build.js` compiles a flat LeanMD document set into one self-contained HTML file.
Markdown, mathematics, document templates, the complete dependency map, map geometry, styles, JavaScript, and KaTeX fonts are embedded in the output.
The output does not read the source document set at run time.

Run the builder from the repository root:

```sh
npm run build:leanmd -- DocumentFormats/LeanMD/__example leanmd-build/__example.html
```

The generated viewer is read-only.
An adjacent `<document>.unresolved` marker is captured as build-time status and displayed in the document toolbar and map, but the generated HTML does not toggle or persist unresolved state.
