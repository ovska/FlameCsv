import { createHighlighter, createJavaScriptRegexEngine } from 'https://esm.sh/shiki@2.3.2'

const highlighter = await createHighlighter({
    themes: ['light-plus', 'dark-plus'],
    langs: ['csharp'],
})

const jsEngine = createJavaScriptRegexEngine();

export default {
    start: () => {
        // console.log(codeToHtml);
    },
    configureHljs: (hljs) => {
        // console.log(hljs);

        const previousHighlightElement = hljs.highlightElement;

        hljs.highlightElement = function(elem) {
            // use previous version if not C#
            if (!elem.classList.contains('lang-cs')) {
                previousHighlightElement.bind(hljs)(elem);
                return;
            }

            const result = highlighter.codeToHtml(elem.textContent, {
                lang: 'csharp',
                engine: jsEngine,
                themes: {
                    light: 'light-plus',
                    dark: 'dark-plus',
                },
            });

            elem.innerHTML = result;
            elem.dataset.highlighted = 'yes';
            elem.className += ' hljs';
            elem.firstChild.style['background-color'] = '#f5f5f5'
        };
    },
}
