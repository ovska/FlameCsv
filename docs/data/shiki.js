import { createHighlighter, createJavaScriptRegexEngine } from 'https://esm.sh/shiki@2.3.2'
// https://esm.sh/@shikijs/transformers@2.3.2

const highlighter = await createHighlighter({
    themes: ['light-plus', 'dark-plus'],
    langs: ['csharp'],
})

const jsEngine = createJavaScriptRegexEngine();

export default { highlighter, jsEngine }
