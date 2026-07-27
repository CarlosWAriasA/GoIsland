import { useEffect } from 'react';

const REVEAL_SELECTOR = '[data-reveal]';
const ENABLED_CLASS = 'js-reveal';

export const useRevealOnScroll = (enabled = true) => {
  useEffect(() => {
    if (!enabled) return;

    const targets = Array.from(document.querySelectorAll<HTMLElement>(REVEAL_SELECTOR));
    if (targets.length === 0) return;

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (prefersReducedMotion || typeof IntersectionObserver === 'undefined') {
      targets.forEach((target) => target.setAttribute('data-reveal', 'shown'));
      return;
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

    targets.forEach((target) => observer.observe(target));

    return () => {
      observer.disconnect();
      root.classList.remove(ENABLED_CLASS);
    };
  }, [enabled]);
};

export default useRevealOnScroll;
