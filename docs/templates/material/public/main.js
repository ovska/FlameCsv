import { createHighlighter, createJavaScriptRegexEngine } from 'https://esm.sh/shiki@2.3.2'
// https://esm.sh/@shikijs/transformers@2.3.2

const highlighter = await createHighlighter({
    themes: ['light-plus', 'dark-plus'],
    langs: ['csharp'],
})

const jsEngine = createJavaScriptRegexEngine();

export default {
    start: () => {
        // add GitHub icon to the navbar
        try {
            // wait for the navbar to be loaded
            const interval = setInterval(() => {
                const navbar = document.querySelector('#navbar form.icons');
                if (navbar || document.querySelector('#github-icon')) {
                    clearInterval(interval);

                    const githubIcon = document.createElement('a');
                    githubIcon.id = 'github-icon';
                    githubIcon.classList.add('bi', 'bi-github');
                    githubIcon.target = '_blank';
                    githubIcon.href = 'https://github.com/ovska/FlameCsv';
                    navbar.insertBefore(githubIcon, navbar.firstChild);
                }
            }, 100);
        } catch (error) {
            console.error('Error adding GitHub icon:', error);
        }
    },
    configureHljs: (hljs) => {
        const previousHighlightElement = hljs.highlightElement;

        hljs.highlightElement = function(elem) {
            // use previous version if not C#
            if (!elem.classList.contains('lang-cs') && !elem.classList.contains('lang-csharp')) {
                previousHighlightElement.bind(hljs)(elem);
                return;
            }

            const code = elem.textContent;

            const result = highlighter.codeToHtml(code, {
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
            elem.firstChild.style['overflow'] = 'visible';

            for (const attr of ["RequiresDynamicCode", "RequiresUnreferencedCode"]) {
                if (code.includes(`[${attr}("`)) {
                    for (const span of elem.querySelectorAll('span:not(.line)')) {
                        if (span.previousElementSibling?.previousElementSibling?.textContent === attr) {
                            span.classList.add('attrhidden');
                            span.style.cursor = 'pointer';

                            // replace the contents of the span with "..." with the attribute
                            // when clicked, toggle between the ellipsis and the original span value,
                            // removing the class if the original value is shown
                            const spanValue = span.textContent;

                            span.title = spanValue;
                            span.textContent = '...';
                            span.onclick = () => {
                                if (span.textContent === '...') {
                                    span.textContent = spanValue;
                                    span.title = '';
                                    span.classList.remove('attrhidden');
                                } else {
                                    span.textContent = '...';
                                    span.title = spanValue;
                                    span.classList.add('attrhidden');
                                }
                            };
                        }
                    }
                }
            }
        };
    },
}
