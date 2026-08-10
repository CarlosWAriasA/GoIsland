import { useEffect } from 'react';

// Solo interesan las secciones que siguen ocultas: las ya reveladas no vuelven a observarse.
const REVEAL_SELECTOR = '[data-reveal]:not([data-reveal="shown"])';
const ENABLED_CLASS = 'js-reveal';

export const useRevealOnScroll = (enabled = true) => {
  useEffect(() => {
    if (!enabled) return;

    const pendingTargets = () => Array.from(document.querySelectorAll<HTMLElement>(REVEAL_SELECTOR));

    // Las secciones que dependen de una consulta más lenta todavía no existen cuando este efecto
    // se ejecuta, y nacían ya ocultas y sin nadie que las observara: quedaban invisibles hasta
    // cambiar de pantalla y volver. Se vuelve a recorrer el documento cada vez que cambia.
    // Se agrupa con un temporizador y no con un fotograma: una pestaña en segundo plano no pinta,
    // y las secciones que llegaran mientras tanto se quedarían sin observar.
    const watchNewTargets = (attach: () => void) => {
      let scheduled = 0;
      const mutations = new MutationObserver(() => {
        if (scheduled) return;
        scheduled = window.setTimeout(() => {
          scheduled = 0;
          attach();
        }, 0);
      });
      mutations.observe(document.body, { childList: true, subtree: true });
      return () => {
        window.clearTimeout(scheduled);
        mutations.disconnect();
      };
    };

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (prefersReducedMotion || typeof IntersectionObserver === 'undefined') {
      const showAll = () => pendingTargets()
        .forEach((target) => target.setAttribute('data-reveal', 'shown'));
      showAll();
      return watchNewTargets(showAll);
    }

    const root = document.documentElement;
    root.classList.add(ENABLED_CLASS);

    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.setAttribute('data-reveal', 'shown');
        observer.unobserve(entry.target);
      });
    }, { rootMargin: '0px 0px -10% 0px', threshold: 0.05 });

    // Observar dos veces el mismo elemento no lo duplica, así que basta con recorrer lo pendiente.
    const observePending = () => pendingTargets().forEach((target) => observer.observe(target));
    observePending();
    const stopWatching = watchNewTargets(observePending);

    return () => {
      stopWatching();
      observer.disconnect();
      root.classList.remove(ENABLED_CLASS);
    };
  }, [enabled]);
};

export default useRevealOnScroll;
