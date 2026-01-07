import { createHighlighter, createJavaScriptRegexEngine } from 'https://cdn.jsdelivr.net/npm/shiki@3.20.0/+esm'

const highlighter = await createHighlighter({
    themes: ['light-plus', 'dark-plus'],
    langs: ['csharp'],
})

const jsEngine = createJavaScriptRegexEngine();

export default { highlighter, jsEngine }
